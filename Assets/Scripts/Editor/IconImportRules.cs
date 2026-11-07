using System;
using UnityEditor;
using UnityEngine;

namespace GeoWar.EditorTools
{
    // Kaynak ikonlarının içe aktarma ayarları: yeni görsel elle ayar gerektirmeden doğru gelir.
    // Şeffaf piksellerin rengi kenarlara sızmaz, küçük gösterimde küçültülmüş sürümler kullanılır.
    public sealed class IconImportRules : AssetPostprocessor
    {
        private const string IconRoot = "Assets/Art/Resources/Icons/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(IconRoot, StringComparison.Ordinal))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
