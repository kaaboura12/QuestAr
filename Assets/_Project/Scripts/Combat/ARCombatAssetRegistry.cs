using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Video;

/// <summary>
/// Runtime-accessible registry stored in Resources so mobile builds have all prefabs,
/// input actions, audio clips, video clips, fonts, and UI sprites that previously
/// relied on editor-only AssetDatabase calls.
/// </summary>
public class ARCombatAssetRegistry : ScriptableObject
{
    public const string ResourcePath = "ARCombatAssetRegistry";

    static ARCombatAssetRegistry instance;

    public static ARCombatAssetRegistry Instance
    {
        get
        {
            if (instance == null)
                instance = Resources.Load<ARCombatAssetRegistry>(ResourcePath);
            return instance;
        }
    }

    [Header("Combat Prefabs & Input")]
    public GameObject fpCharacterPrefab;
    public GameObject handgunPrefab;
    public GameObject zombiePrefab;
    public GameObject uiCanvasPrefab;
    public InputActionAsset inputActions;

    [Header("Audio & Video")]
    public AudioClip hurtVoice;
    public VideoClip endClip;

    [Header("Fonts")]
    public TMP_FontAsset robotoBoldFont;
    public TMP_FontAsset medievalSharpFont;

    [Header("Vitals HUD")]
    public Sprite healthFill;
    public Sprite healthTrack;

    [Header("Settings Panel Sprites")]
    public Sprite settingsFrame;
    public Sprite settingsWell;
    public Sprite settingsGear;
    public Sprite settingsClose;
    public Sprite settingsTrack;
    public Sprite settingsFill;
    public Sprite settingsHandle;
    public Sprite settingsToggle;
    public Sprite settingsMark;
    public Sprite settingsButton;
    public Sprite settingsButtonHover;
    public Sprite settingsButtonPressed;

    public Sprite GetSpriteByAssetPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        switch (path)
        {
            case "Assets/Alebardium/Bloodlines UI/Textures/Progress_Bar/Rectangle/Progress_Bar_Rectangle_full_v1.png":
                return healthFill;
            case "Assets/Alebardium/Bloodlines UI/Textures/Progress_Bar/Rectangle/Progress_Bar_Rectangle_empty_v1.png":
                return healthTrack;
            case "Assets/Alebardium/Bloodlines UI/Textures/Frame/Frame_main_menu_red.png":
                return settingsFrame;
            case "Assets/Alebardium/Bloodlines UI/Textures/Frame/Frame_outline_red.png":
                return settingsWell;
            case "Assets/Alebardium/Bloodlines UI/Textures/Icon/Icons_settins_frame.png":
                return settingsGear;
            case "Assets/Alebardium/Bloodlines UI/Textures/Input Field/Cross.png":
                return settingsClose;
            case "Assets/Alebardium/Bloodlines UI/Textures/Progress_Bar/Rectangle (Segments)/Slider_empty.png":
                return settingsTrack;
            case "Assets/Alebardium/Bloodlines UI/Textures/Progress_Bar/Rectangle (Segments)/Slider_full_v1.png":
                return settingsFill;
            case "Assets/Alebardium/Bloodlines UI/Textures/Progress_Bar/Rectangle (Segments)/Slider_toggler.png":
                return settingsHandle;
            case "Assets/Alebardium/Bloodlines UI/Textures/Toggle/Icons-13.png":
                return settingsToggle;
            case "Assets/Alebardium/Bloodlines UI/Textures/Toggle/Icon Checkmark 1 (Round).png":
                return settingsMark;
            case "Assets/Alebardium/Bloodlines UI/Textures/Button/Button2/Button2_Status_Red_Default.png":
                return settingsButton;
            case "Assets/Alebardium/Bloodlines UI/Textures/Button/Button2/Button2_Status_Red_Hover.png":
                return settingsButtonHover;
            case "Assets/Alebardium/Bloodlines UI/Textures/Button/Button2/Button2_Status_Pressed.png":
                return settingsButtonPressed;
            default:
                return null;
        }
    }
}
