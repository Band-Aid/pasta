using UnityEditor;
using UnityEngine;

namespace PastaSurvivors.EditorTools
{
    /// <summary>
    /// Imports new tracks in Resources/Music as streamed Vorbis so a full song never sits decompressed in memory.
    /// Only the first import is touched; later changes in the inspector are kept.
    /// </summary>
    public class MusicImport : AssetPostprocessor
    {
        private const string Folder = "Assets/Survivors/Resources/Music/";

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Folder) || !assetImporter.importSettingsMissing) return;
            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            importer.defaultSampleSettings = settings;
        }
    }
}
