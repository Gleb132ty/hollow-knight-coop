using System;
using Hkmp.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Logger = Hkmp.Logging.Logger;

namespace Hkmp.Ui.Resources;

/// <summary>
/// The font manager that stores fonts that are used in-game.
/// </summary>
internal static class FontManager {
    /// <summary>
    /// The font used for UI.
    /// </summary>
    public static Font UIFontRegular;

    /// <summary>
    /// The font used for usernames above player objects.
    /// </summary>
    public static TMP_FontAsset InGameNameFont;

    /// <summary>
    /// Load the fonts by trying to find them in the game through Unity. If the current game language is Russian,
    /// a font that is able to render Cyrillic characters is selected as well.
    /// </summary>
    public static void LoadFonts() {
        Font latinFont = null;
        foreach (var font in UnityEngine.Resources.FindObjectsOfTypeAll<Font>()) {
            if (font.name == "Perpetua") {
                latinFont = font;
            }
        }

        TMP_FontAsset latinNameFont = null;
        foreach (var textMeshProFont in UnityEngine.Resources.FindObjectsOfTypeAll<TMP_FontAsset>()) {
            if (textMeshProFont.name == "TrajanPro-Bold SDF") {
                latinNameFont = textMeshProFont;
            }
        }

        if (Localization.IsRussian) {
            UIFontRegular = FindCyrillicFont() ?? CreateCyrillicFont() ?? latinFont;
            InGameNameFont = FindCyrillicNameFont() ?? latinNameFont;
        } else {
            UIFontRegular = latinFont;
            InGameNameFont = latinNameFont;
        }

        if (UIFontRegular == null) {
            Logger.Error("UI font regular is missing!");
        }

        if (InGameNameFont == null) {
            Logger.Error("In-game name font is missing!");
        }
    }

    /// <summary>
    /// Apply the currently loaded fonts to all UI elements that were already created. Used when the game language
    /// changes at runtime, so existing text switches to a font that can render the new characters.
    /// </summary>
    public static void ApplyFontsToExistingUi() {
        if (UIFontRegular != null && UiManager.UiGameObject != null) {
            foreach (var text in UiManager.UiGameObject.GetComponentsInChildren<Text>(true)) {
                if (text != null) {
                    text.font = UIFontRegular;
                }
            }
        }

        if (InGameNameFont != null) {
            foreach (var textMeshPro in UnityEngine.Object.FindObjectsOfType<TextMeshPro>()) {
                if (textMeshPro != null && textMeshPro.font != InGameNameFont) {
                    textMeshPro.font = InGameNameFont;
                }
            }
        }
    }

    /// <summary>
    /// Find a font that is already loaded by the game and is able to render Cyrillic characters.
    /// </summary>
    /// <returns>The font, or null if no such font was found.</returns>
    private static Font FindCyrillicFont() {
        foreach (var font in UnityEngine.Resources.FindObjectsOfTypeAll<Font>()) {
            if (font == null || !font.dynamic) {
                continue;
            }

            if (HasCyrillicCharacters(font)) {
                return font;
            }
        }

        return null;
    }

    /// <summary>
    /// Create a dynamic font from a system font that is able to render Cyrillic characters.
    /// </summary>
    /// <returns>The font, or null if no system font could be loaded.</returns>
    private static Font CreateCyrillicFont() {
        try {
            var font = Font.CreateDynamicFontFromOSFont(
                ["Segoe UI", "Arial", "Tahoma", "Verdana", "Microsoft Sans Serif"],
                32
            );

            if (font != null) {
                return font;
            }
        } catch (Exception exception) {
            Logger.Error($"Could not create dynamic font from OS font: {exception}");
        }

        return null;
    }

    /// <summary>
    /// Find a TMP font asset that is able to render Cyrillic characters. This first tries the Russian font that the
    /// game itself uses and otherwise checks all loaded font assets.
    /// </summary>
    /// <returns>The font asset, or null if no such font asset was found.</returns>
    private static TMP_FontAsset FindCyrillicNameFont() {
        foreach (var changeFontByLanguage in UnityEngine.Resources.FindObjectsOfTypeAll<ChangeFontByLanguage>()) {
            if (changeFontByLanguage == null || changeFontByLanguage.fontRU == null) {
                continue;
            }

            if (HasCyrillicCharacter(changeFontByLanguage.fontRU)) {
                return changeFontByLanguage.fontRU;
            }
        }

        foreach (var textMeshProFont in UnityEngine.Resources.FindObjectsOfTypeAll<TMP_FontAsset>()) {
            if (textMeshProFont != null && HasCyrillicCharacter(textMeshProFont)) {
                return textMeshProFont;
            }
        }

        return null;
    }

    /// <summary>
    /// Whether the given font is able to render basic Cyrillic characters.
    /// </summary>
    /// <param name="font">The font to check.</param>
    /// <returns>true if the font supports Cyrillic characters, false otherwise.</returns>
    private static bool HasCyrillicCharacters(Font font) {
        try {
            return font.HasCharacter('А') && font.HasCharacter('я');
        } catch (Exception) {
            return false;
        }
    }

    /// <summary>
    /// Whether the given TMP font asset is able to render basic Cyrillic characters.
    /// </summary>
    /// <param name="fontAsset">The font asset to check.</param>
    /// <returns>true if the font asset supports Cyrillic characters, false otherwise.</returns>
    private static bool HasCyrillicCharacter(TMP_FontAsset fontAsset) {
        try {
            return fontAsset.HasCharacter('А') && fontAsset.HasCharacter('я');
        } catch (Exception) {
            return false;
        }
    }
}
