using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PastaSurvivors
{
    /// <summary>
    /// Player visuals. Third person: an animated body holding the current main weapon.
    /// First person: a view model of two hands in front of the camera. Every Spaghetti attack
    /// visibly bends and breaks a real bundle of spaghetti in both views.
    /// </summary>
    public class PlayerAnim : MonoBehaviour
    {
        public const float EyeHeight = 1.8f;

        private Rig rig;
        private Transform bodyAnchor, lasagna, marker;
        private Transform viewRoot, viewAnchor, viewHandL, viewHandR;
        private readonly List<Renderer> viewRenderers = new List<Renderer>();
        private PastaVisual bundle;
        private GameObject heldItem, beamCup;
        private bool beaming;
        private WeaponId? shownMain;
        private bool shownFirstPerson, firstPerson;
        private float phase, throwT, slamT, snapT, respawnT, switchT;
        private bool flashing;
        private Quaternion facingRot = Quaternion.identity;

        private Transform Anchor => firstPerson ? viewAnchor : bodyAnchor;

        public void Build(CharacterDef c, int index)
        {
            rig = Models.Build(Models.Player(c, index), transform, "Model");
            rig.root.localScale = Vector3.one * 1.15f;
            bodyAnchor = new GameObject("Held").transform;
            bodyAnchor.SetParent(rig.torso, false);
            bodyAnchor.localPosition = new Vector3(0f, 1.02f, 0.55f);
            bodyAnchor.localScale = Vector3.one * 1.5f;

            var lg = Models.Static(Models.Get("lasagna"), rig.torso, "Lasagna sheet");
            lasagna = lg.transform;
            lasagna.localScale = Vector3.one * 1.8f;
            lasagna.gameObject.SetActive(false);

            var ring = Models.Static(MeshKit.Sector(0.7f, 0.9f, 360f, 40, "Player ring"), transform, "Marker", Mats.FxAdd, false);
            marker = ring.transform;
            marker.localPosition = new Vector3(0f, 0.05f, 0f);
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_BaseColor", new Color(1f, 0.85f, 0.35f, 1f));
            ring.GetComponent<MeshRenderer>().SetPropertyBlock(mpb);

            BuildViewModel(c);
        }

        private void BuildViewModel(CharacterDef c)
        {
            if (G.Cam == null) return;
            viewRoot = new GameObject("View model").transform;
            viewRoot.SetParent(G.Cam.transform, false);
            viewRoot.localPosition = new Vector3(0f, -0.3f, 0.62f);
            viewAnchor = new GameObject("View held").transform;
            viewAnchor.SetParent(viewRoot, false);
            viewAnchor.localScale = Vector3.one * 0.95f;
            var kit = new MeshKit();
            kit.Bar(new Vector3(0f, -0.08f, -0.42f), new Vector3(0f, 0f, -0.04f), 0.11f, c.shirt);
            kit.Ball(new Vector3(0f, 0f, 0f), new Vector3(0.1f, 0.085f, 0.12f), c.skin);
            kit.Ball(new Vector3(0f, 0.035f, 0.03f), new Vector3(0.05f, 0.04f, 0.05f), c.skin);
            var handMesh = kit.ToMesh("View hand");
            viewHandL = Models.Static(handMesh, viewRoot, "HandL", Mats.Lit, false).transform;
            viewHandR = Models.Static(handMesh, viewRoot, "HandR", Mats.Lit, false).transform;
            viewRenderers.Add(viewHandL.GetComponent<Renderer>());
            viewRenderers.Add(viewHandR.GetComponent<Renderer>());
            viewRoot.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (viewRoot != null) Destroy(viewRoot.gameObject);
        }

        public void SetFirstPerson(bool on)
        {
            firstPerson = on;
            if (viewRoot != null) viewRoot.gameObject.SetActive(on);
            marker.gameObject.SetActive(!on);
            foreach (var r in rig.renderers) r.shadowCastingMode = on ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            if (lasagna != null) lasagna.GetComponent<Renderer>().shadowCastingMode = on ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            shownMain = null; // rebuild the held item in the right place
        }

        // ---------------- held item ----------------

        private void RefreshHeld(bool force = false)
        {
            var main = G.Player != null ? G.Player.Main : null;
            WeaponId? want = main != null ? main.Id : (WeaponId?)null;
            if (!force && want == shownMain && shownFirstPerson == firstPerson) return;
            if (snapT > 0f) return; // finish the snap first
            shownMain = want;
            shownFirstPerson = firstPerson;
            ClearHeld();
            if (want == null) return;
            if (want == WeaponId.SpaghettiSnap) { SpawnBundle(false); return; }
            Mesh mesh; float scale = 1f; Vector3 euler = Vector3.zero;
            switch (want.Value)
            {
                case WeaponId.PenneShot: mesh = Models.Shot(ShotKind.Penne); scale = 1.1f; euler = new Vector3(0, 90, 0); break;
                case WeaponId.LasagnaCrash: mesh = Models.Get("lasagna"); scale = 0.8f; euler = new Vector3(-20, 0, 0); break;
                case WeaponId.FusilliOrbit: mesh = Models.Shot(ShotKind.Fusilli); scale = 0.8f; euler = new Vector3(0, 90, 0); break;
                case WeaponId.FarfalleBoomerang: mesh = Models.Shot(ShotKind.Farfalle); scale = 0.8f; euler = new Vector3(-60, 0, 0); break;
                case WeaponId.KetchupBomb: mesh = Models.Shot(ShotKind.Ketchup); scale = 0.9f; euler = new Vector3(-15, 0, -20); break;
                case WeaponId.PineapplePizza: mesh = Models.Shot(ShotKind.Pizza); scale = 0.45f; euler = new Vector3(-55, 0, 0); break;
                default: mesh = Models.Get("carbonara"); scale = 0.5f; euler = new Vector3(-35, 0, 0); break;
            }
            heldItem = Models.Static(mesh, Anchor, "Held " + want.Value, Mats.Lit, !firstPerson);
            // In first person the item sits in the right hand, small enough to keep the crosshair clear.
            heldItem.transform.localPosition = firstPerson ? new Vector3(0.15f, -0.02f, 0.05f) : new Vector3(0f, 0.03f, 0.02f);
            heldItem.transform.localRotation = Quaternion.Euler(euler);
            heldItem.transform.localScale = Vector3.one * scale * (firstPerson ? 0.4f : 1f);
            var r = heldItem.GetComponent<Renderer>();
            if (firstPerson) viewRenderers.Add(r);
        }

        private void ClearHeld()
        {
            if (beamCup != null) { Destroy(beamCup); beamCup = null; }
            beaming = false;
            if (heldItem != null)
            {
                viewRenderers.Remove(heldItem.GetComponent<Renderer>());
                Destroy(heldItem);
            }
            heldItem = null;
            if (bundle != null) Destroy(bundle.gameObject);
            bundle = null;
        }

        private void SpawnBundle(bool temporary)
        {
            SpawnBundle();
        }

        private void SpawnBundle()
        {
            if (bundle != null) Destroy(bundle.gameObject);
            var go = new GameObject("Spaghetti bundle");
            go.transform.SetParent(Anchor, false);
            bundle = go.AddComponent<PastaVisual>();
            bundle.Initialize(PastaType.Spaghetti);
        }

        // ---------------- triggers ----------------

        /// <summary>Spaghetti attack: bend and break a bundle (pulling one out if another weapon is in hand).</summary>
        public void Snap()
        {
            if (snapT > 0f) return;
            if (bundle == null)
            {
                if (heldItem != null) heldItem.SetActive(false);
                SpawnBundle(true);
            }
            snapT = 0.16f;
        }

        public void Throw() => throwT = 0.28f;

        /// <summary>Hold the giant parody frappé out in front while the Sta○ Beam fires.</summary>
        public void SetBeaming(bool on)
        {
            beaming = on;
            if (on)
            {
                if (beamCup == null) beamCup = Models.Static(Models.Get("special_StarBeam"), Anchor, "Frappe", Mats.Lit, !firstPerson);
                beamCup.transform.SetParent(Anchor, false);
                beamCup.transform.localPosition = firstPerson ? new Vector3(0.05f, 0f, 0.1f) : new Vector3(0f, 0f, 0.1f);
                beamCup.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
                beamCup.transform.localScale = Vector3.one * (firstPerson ? 0.55f : 0.9f);
                beamCup.SetActive(true);
            }
            else if (beamCup != null) beamCup.SetActive(false);
            if (heldItem != null) heldItem.SetActive(!on);
            if (bundle != null) bundle.gameObject.SetActive(!on);
        }
        public void Slam() => slamT = 0.4f;
        public void OnSwitch() => switchT = 0.25f;

        /// <summary>Topples over when defeated.</summary>
        public void Fall(float dt)
        {
            var target = facingRot * Quaternion.Euler(-85f, 0f, 0f);
            rig.root.localRotation = Quaternion.Slerp(rig.root.localRotation, target, 1f - Mathf.Exp(-6f * dt));
            rig.armL.localRotation = Quaternion.Slerp(rig.armL.localRotation, Quaternion.Euler(-170f, 0f, -30f), dt * 4f);
            rig.armR.localRotation = Quaternion.Slerp(rig.armR.localRotation, Quaternion.Euler(-170f, 0f, 30f), dt * 4f);
            if (viewRoot != null) viewRoot.localPosition = Vector3.Lerp(viewRoot.localPosition, new Vector3(0f, -0.9f, 0.5f), dt * 3f);
        }

        // ---------------- per frame ----------------

        public void Tick(float dt, float speed, Vector3 facing, bool hurt, bool dashing)
        {
            RefreshHeld();
            if (facing.sqrMagnitude > 0.01f)
                facingRot = Quaternion.Slerp(facingRot, Quaternion.LookRotation(facing), 1f - Mathf.Exp(-20f * dt));
            rig.root.localRotation = facingRot * Quaternion.Euler(dashing ? 18f : 0f, 0f, 0f);

            float amp = Mathf.Clamp01(speed / 4f);
            phase += dt * (6f + speed * 1.6f);
            float s = Mathf.Sin(phase);
            rig.legL.localRotation = Quaternion.Euler(s * 38f * amp, 0f, 0f);
            rig.legR.localRotation = Quaternion.Euler(-s * 38f * amp, 0f, 0f);
            rig.torso.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Cos(phase)) * 0.06f * amp, 0f);

            float armL = rig.armRest + s * 6f * amp, armR = rig.armRest - s * 6f * amp;
            float zL = -6f, zR = 6f;
            float bendK = 0f;

            // Snap: bend the bundle, then break it.
            if (snapT > 0f)
            {
                snapT -= dt;
                bendK = 1f - Mathf.Clamp01(snapT / 0.16f);
                if (bundle != null) bundle.Bend(bendK);
                armL += bendK * 20f; armR += bendK * 20f;
                zL = -6f - bendK * 25f; zR = 6f + bendK * 25f;
                if (snapT <= 0f && bundle != null)
                {
                    bundle.transform.SetParent(null, true);
                    bundle.Shatter(facing * 2.5f + Vector3.up * 1.5f);
                    Destroy(bundle.gameObject, 0.05f);
                    bundle = null;
                    respawnT = 0.3f;
                }
            }
            else if (bundle == null && respawnT > 0f)
            {
                respawnT -= dt;
                if (respawnT <= 0f)
                {
                    bool mainIsSpaghetti = shownMain == WeaponId.SpaghettiSnap;
                    if (mainIsSpaghetti) SpawnBundle(false);
                    else if (heldItem != null) heldItem.SetActive(true);
                    else RefreshHeld(true);
                }
            }

            float throwK = 0f;
            if (throwT > 0f)
            {
                throwT -= dt;
                throwK = throwT / 0.28f;
                armR = Mathf.Lerp(-20f, -165f, throwK * throwK);
            }
            float slamUp = 0f;
            if (slamT > 0f)
            {
                slamT -= dt;
                float k = slamT / 0.4f;
                float up = k > 0.5f ? Mathf.Lerp(-175f, -120f, (k - 0.5f) * 2f) : Mathf.Lerp(-30f, -175f, k * 2f);
                slamUp = k > 0.5f ? 1f : k * 2f;
                armL = armR = up;
                lasagna.gameObject.SetActive(!firstPerson);
                lasagna.localPosition = new Vector3(0f, 1.3f + Mathf.Sin(-up * Mathf.Deg2Rad) * 0.8f, Mathf.Cos(-up * Mathf.Deg2Rad) * 0.8f + 0.3f);
                lasagna.localRotation = Quaternion.Euler(-up - 90f, 0f, 0f);
            }
            else if (lasagna.gameObject.activeSelf) lasagna.gameObject.SetActive(false);

            if (beaming) { armL = armR = -88f; zL = -18f; zR = 18f; }
            rig.armL.localRotation = Quaternion.Euler(armL, 0f, zL);
            rig.armR.localRotation = Quaternion.Euler(armR, 0f, zR);

            if (firstPerson) TickView(dt, speed, bendK, throwK, slamUp);

            if (flashing != hurt)
            {
                flashing = hurt;
                var m = hurt ? Mats.Flash : Mats.Lit;
                foreach (var r in rig.renderers) r.sharedMaterial = m;
                foreach (var r in viewRenderers) if (r != null) r.sharedMaterial = m;
            }
            marker.localRotation = Quaternion.Euler(0f, Time.time * 40f, 0f);
        }

        private void TickView(float dt, float speed, float bendK, float throwK, float slamUp)
        {
            if (viewRoot == null) return;
            float amp = Mathf.Clamp01(speed / 5f);
            var bob = new Vector3(Mathf.Sin(phase * 0.5f) * 0.025f * amp, -Mathf.Abs(Mathf.Cos(phase * 0.5f)) * 0.03f * amp, 0f);
            switchT = Mathf.Max(0f, switchT - dt);
            float dip = Mathf.Sin(Mathf.Clamp01(switchT / 0.25f) * Mathf.PI) * 0.25f;
            var pos = new Vector3(0f, -0.3f - dip + slamUp * 0.25f, 0.62f + throwK * 0.12f) + bob;
            viewRoot.localPosition = Vector3.Lerp(viewRoot.localPosition, pos, 1f - Mathf.Exp(-25f * dt));
            viewRoot.localRotation = Quaternion.Euler(-throwK * 25f - slamUp * 35f + dip * 60f, 0f, 0f);
            // Hands grip the ends of whatever is held; they pull apart while the spaghetti bends.
            Vector3 l, r;
            if (bundle != null)
            {
                l = viewAnchor.localPosition + bundle.Point(-1f) * viewAnchor.localScale.x;
                r = viewAnchor.localPosition + bundle.Point(1f) * viewAnchor.localScale.x;
            }
            else { l = new Vector3(-0.24f, -0.1f, -0.04f); r = new Vector3(0.15f, -0.05f, 0.02f); }
            viewHandL.localPosition = Vector3.Lerp(viewHandL.localPosition, l + new Vector3(0f, -0.02f, 0f), 1f - Mathf.Exp(-30f * dt));
            viewHandR.localPosition = Vector3.Lerp(viewHandR.localPosition, r + new Vector3(0f, -0.02f, 0f), 1f - Mathf.Exp(-30f * dt));
            viewHandL.localRotation = Quaternion.Euler(0f, 18f, bendK * 50f);
            viewHandR.localRotation = Quaternion.Euler(0f, -18f, -bendK * 50f);
        }
    }
}
