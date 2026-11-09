using UnityEngine;

namespace PastaSurvivors
{
    public enum IngredientId { Pasta, Ketchup, Pizza, Pineapple }
    public enum RecipeId { Napolitan, PineapplePizza }

    public class RecipeDef
    {
        public RecipeId id;
        public string name, icon, desc;
        public WeaponId weapon;
        public IngredientId basis, topping;
        public Color color;
    }

    public static class CraftingData
    {
        public static readonly RecipeDef[] Recipes =
        {
            new RecipeDef
            {
                id = RecipeId.Napolitan, name = "ナポリタン", icon = "napolitan",
                desc = "折るたびに赤い炎の軌跡！ 扇の範囲が広がり、敵を強く吹き飛ばす。",
                weapon = WeaponId.SpaghettiSnap, basis = IngredientId.Pasta, topping = IngredientId.Ketchup,
                color = new Color(1f, 0.3f, 0.12f)
            },
            new RecipeDef
            {
                id = RecipeId.PineapplePizza, name = "パインピザ", icon = "ragepizza",
                desc = "命中した敵が激怒して突進！ 進路の仲間を巻き込み、ダメージを与える（ボスを除く）。",
                weapon = WeaponId.PineapplePizza, basis = IngredientId.Pizza, topping = IngredientId.Pineapple,
                color = new Color(1f, 0.85f, 0.2f)
            }
        };

        public static string Name(IngredientId id) => id switch
        {
            IngredientId.Pasta => "パスタ",
            IngredientId.Ketchup => "ケチャップ",
            IngredientId.Pizza => "ピザ生地",
            _ => "パイナップル"
        };

        public static string Icon(IngredientId id) => id switch
        {
            IngredientId.Pasta => "spaghetti",
            IngredientId.Ketchup => "ketchup",
            IngredientId.Pizza => "pizzaslice",
            _ => "pineapple"
        };
    }

    /// <summary>Run-local food supplies. Crafting modifies a base weapon in place, independently of evolution.</summary>
    public class CraftingInventory
    {
        public const int Capacity = 9;
        private readonly int[] amounts = new int[4];
        private readonly Player player;

        public CraftingInventory(Player player) => this.player = player;
        public int Count(IngredientId id) => amounts[(int)id];
        public void Clear() => System.Array.Clear(amounts, 0, amounts.Length);

        public bool Add(IngredientId id)
        {
            if (Count(id) >= Capacity) return false;
            amounts[(int)id]++;
            return true;
        }

        public bool Needs(IngredientId id)
        {
            if (Count(id) > 0) return false;
            foreach (var recipe in CraftingData.Recipes)
            {
                var weapon = player.GetWeapon(recipe.weapon);
                if (weapon != null && weapon.Recipe != null) continue;
                if (recipe.topping == id || (recipe.basis == id && weapon == null)) return true;
            }
            return false;
        }

        public bool CanCraft(RecipeDef recipe, out string reason)
        {
            var weapon = player.GetWeapon(recipe.weapon);
            if (weapon != null && weapon.Recipe != null) { reason = "合成済み"; return false; }
            bool hasBase = weapon != null || Count(recipe.basis) > 0;
            bool hasTopping = Count(recipe.topping) > 0;
            if (!hasBase || !hasTopping)
            {
                reason = "不足：" + (!hasBase ? CraftingData.Name(recipe.basis) : "")
                    + (!hasBase && !hasTopping ? " ＋ " : "") + (!hasTopping ? CraftingData.Name(recipe.topping) : "");
                return false;
            }
            if (weapon == null && player.Weapons.Count >= Player.MaxWeapons)
            {
                reason = "武器枠が満杯（素材は消費しません）";
                return false;
            }
            reason = weapon != null ? "合成する（所持武器を加工）" : "合成する（武器枠を1つ使用）";
            return true;
        }

        public int ReadyCount
        {
            get
            {
                int count = 0;
                foreach (var recipe in CraftingData.Recipes) if (CanCraft(recipe, out _)) count++;
                return count;
            }
        }

        public bool TryCraft(RecipeDef recipe)
        {
            if (!CanCraft(recipe, out _)) return false;
            var weapon = player.GetWeapon(recipe.weapon);
            if (weapon == null)
            {
                player.AddWeapon(recipe.weapon);
                weapon = player.GetWeapon(recipe.weapon);
                if (weapon == null) return false;
                amounts[(int)recipe.basis]--;
            }
            amounts[(int)recipe.topping]--;
            weapon.ApplyRecipe(recipe);
            return true;
        }
    }
}
