using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.IO;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;

[System.Serializable]
public class VoiceActor
{
    public string character;
    public string actor;
}

[System.Serializable]
public class AudioSubItem
{
    public string name;
    public string members;
}

[System.Serializable]
public class VoiceActorItem
{
    public string name;
    public VoiceActor[] members;
}

[System.Serializable]
public class AudioProduction
{
    public string name;
    public AudioSubItem company;
    public AudioSubItem music_director;
    public AudioSubItem composer;
    public AudioSubItem sound_design;
    public AudioSubItem mixing;
    public AudioSubItem sound_producer;
    public AudioSubItem voice_director;
    public VoiceActorItem voice_actors;
}

[System.Serializable]
public class MultiMemberItem
{
    public string name;
    public string[] members;
}

[System.Serializable]
public class CreditsData
{
    public MultiMemberItem producers;
    public MultiMemberItem distributors;
    public MultiMemberItem executive_producer;
    public MultiMemberItem supervising_unit;
    public MultiMemberItem director_writer;
    public MultiMemberItem executive_producers;
    public MultiMemberItem academic_advisors;
    public MultiMemberItem concept_design;
    public MultiMemberItem animation;
    public MultiMemberItem environment;
    public MultiMemberItem assets;
    public MultiMemberItem development_team;
    public MultiMemberItem technical_art;
    public MultiMemberItem vfx;
    public MultiMemberItem visual_design;
    public MultiMemberItem brand_marketing;
    public MultiMemberItem operations_team;
    public AudioProduction audio_production;
}

[System.Serializable]
public class CreditsRoot
{
    public CreditsData credits;
}

[System.Serializable]
public class FontConfig
{
    public string fontColor;
    public int fontSize;
    public string fontStyle;
}

[System.Serializable]
public class PaddingConfig
{
    public int left;
    public int right;
    public int top;
    public int bottom;
}

[System.Serializable]
public class SectionConfig
{
    public int spacing;
    public PaddingConfig padding;
}

[System.Serializable]
public class CreditsStyleData
{
    public FontConfig title;
    public FontConfig subtitle;
    public FontConfig textItem;
    public SectionConfig section;
}

[System.Serializable]
public class CreditsStyleRoot
{
    public CreditsStyleData creditsStyle;
}









public class CreditsGenerator : MonoBehaviour
{
    [Header("UI Settings")]
    public Transform contentParent;
    public TMP_FontAsset fontAsset;

    [Header("File Settings")]
    public string jsonFileName = "credits.json";
    public string jsonStyleFileName = "creditsStyle.json";

    private CreditsRoot creditsRoot;
    private CreditsStyleRoot creditsStyleRoot;


    public enum LayoutType { Horizion, Vertical}
    private LayoutType type;
    void Start()
    {
        
        if (contentParent == null)
        {
            Debug.LogError("Content Parent is not set. Please assign it in the inspector.");
            return;
        }

        if (fontAsset == null)
        {
            Debug.LogError("Font Asset is not set. Please assign it in the inspector.");
            return;
        }

        DeleteUI();
        GeneratUI();

     }


    private void OnDestroy()
    {
        DeleteUI();
    }


    public void GeneratUI()
    {
        
        LoadCreditsData();
        LoadCreditsStyleData();
        GenerateCreditsUI();
    }


    public void DeleteUI()
    {
        if (contentParent.childCount >= 1)
        {
            for (int i = contentParent.childCount - 1; i >= 0; i--)
            {
                GameObject child = contentParent.GetChild(i).gameObject;
#if UNITY_EDITOR
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
#else
            Destroy(child);
#endif
            }
        }
  
    }

    void LoadCreditsData()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, jsonFileName);
        
        if (File.Exists(filePath))
        {
            string jsonContent = File.ReadAllText(filePath);
            creditsRoot = JsonUtility.FromJson<CreditsRoot>(jsonContent);
        }
        else
        {
            Debug.LogError($"Credits JSON file not found at: {filePath}");
        }
    }

    void LoadCreditsStyleData()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, jsonStyleFileName);

        if (File.Exists(filePath))
        {
            string jsonContent = File.ReadAllText(filePath);
            creditsStyleRoot = JsonUtility.FromJson<CreditsStyleRoot>(jsonContent);
        }
        else
        {
            Debug.LogError($"Style JSON file not found at: {filePath}");
            // 设置默认样式
            SetDefaultStyle();
        }
    }

    void SetDefaultStyle()
    {
        creditsStyleRoot = new CreditsStyleRoot();
        creditsStyleRoot.creditsStyle = new CreditsStyleData();
        creditsStyleRoot.creditsStyle.title = new FontConfig { fontColor = "#FFFFFF", fontSize = 24, fontStyle = "Bold" };
        creditsStyleRoot.creditsStyle.subtitle = new FontConfig { fontColor = "#FFFFFF", fontSize = 18, fontStyle = "Bold" };
        creditsStyleRoot.creditsStyle.textItem = new FontConfig { fontColor = "#FFFFFF", fontSize = 14, fontStyle = "Normal" };
        creditsStyleRoot.creditsStyle.section = new SectionConfig 
        { 
            spacing = 5, 
            padding = new PaddingConfig { left = 0, right = 0, top = 35, bottom = 0 } 
        };
    }

    void GenerateCreditsUI()
    {
        if (creditsRoot?.credits == null) return;

        // 读取JSON文件以获取字段顺序
        string filePath = Path.Combine(Application.streamingAssetsPath, jsonFileName);
        if (!File.Exists(filePath)) return;

        string jsonContent = File.ReadAllText(filePath);
        JObject jsonObject = JObject.Parse(jsonContent);
        JObject creditsObject = jsonObject["credits"] as JObject;

        if (creditsObject == null) return;

        var credits = creditsRoot.credits;

        Dictionary<string, LayoutType> fieldLayouts = new Dictionary<string, LayoutType>
        {
            {"producers", LayoutType.Vertical},
            {"distributors", LayoutType.Vertical},
            {"executive_producer", LayoutType.Vertical},
            {"supervising_unit", LayoutType.Vertical},
            {"director_writer", LayoutType.Vertical},
            {"executive_producers", LayoutType.Horizion},
            {"academic_advisors", LayoutType.Horizion},
            {"concept_design", LayoutType.Horizion},
            {"animation", LayoutType.Horizion},
            {"environment", LayoutType.Horizion},
            {"assets", LayoutType.Horizion},
            {"development_team", LayoutType.Horizion},
            {"technical_art_vfx", LayoutType.Horizion},
            {"visual_design", LayoutType.Horizion},
            {"brand_marketing", LayoutType.Horizion},
            {"operations_team", LayoutType.Horizion},
            {"audio_production", LayoutType.Vertical}
        };

        foreach (var property in creditsObject.Properties())
        {
            string fieldName = property.Name;
            LayoutType layout = fieldLayouts.ContainsKey(fieldName) ? fieldLayouts[fieldName] : LayoutType.Vertical;

            // 根据字段名处理不同的数据类型
            switch (fieldName)
            {
                case "producers":
                    if (credits.producers != null)
                        CreateSection(credits.producers.name, credits.producers.members, layout);
                    break;
                case "distributors":
                    if (credits.distributors != null)
                        CreateSection(credits.distributors.name, credits.distributors.members, layout);
                    break;
                case "executive_producer":
                    if (credits.executive_producer != null)
                        CreateSection(credits.executive_producer.name, credits.executive_producer.members, layout);
                    break;
                case "supervising_unit":
                    if (credits.supervising_unit != null)
                        CreateSection(credits.supervising_unit.name, credits.supervising_unit.members, layout);
                    break;
                case "director_writer":
                    if (credits.director_writer != null)
                        CreateSection(credits.director_writer.name, credits.director_writer.members, layout);
                    break;
                case "executive_producers":
                    if (credits.executive_producers != null)
                        CreateSection(credits.executive_producers.name, credits.executive_producers.members, layout);
                    break;
                case "academic_advisors":
                    if (credits.academic_advisors != null)
                        CreateSection(credits.academic_advisors.name, credits.academic_advisors.members, layout);
                    break;
                case "concept_design":
                    if (credits.concept_design != null)
                        CreateSection(credits.concept_design.name, credits.concept_design.members, layout);
                    break;
                case "animation":
                    if (credits.animation != null)
                        CreateSection(credits.animation.name, credits.animation.members, layout);
                    break;
                case "environment":
                    if (credits.environment != null)
                        CreateSection(credits.environment.name, credits.environment.members, layout);
                    break;
                case "assets":
                    if (credits.assets != null)
                        CreateSection(credits.assets.name, credits.assets.members, layout);
                    break;
                case "development_team":
                    if (credits.development_team != null)
                        CreateSection(credits.development_team.name, credits.development_team.members, layout);
                    break;
                case "technical_art":
                    if (credits.technical_art != null)
                        CreateSection(credits.technical_art.name, credits.technical_art.members, layout);
                    break;
                case "vfx":
                    if (credits.vfx != null)
                        CreateSection(credits.vfx.name, credits.vfx.members, layout);
                    break;
                case "visual_design":
                    if (credits.visual_design != null)
                        CreateSection(credits.visual_design.name, credits.visual_design.members, layout);
                    break;
                case "brand_marketing":
                    if (credits.brand_marketing != null)
                        CreateSection(credits.brand_marketing.name, credits.brand_marketing.members, layout);
                    break;
                case "operations_team":
                    if (credits.operations_team != null)
                        CreateSection(credits.operations_team.name, credits.operations_team.members, layout);
                    break;
                case "audio_production":
                    if (credits.audio_production != null)
                        CreateAudioProductionSection(credits.audio_production.name, credits.audio_production);
                    break;
            }
        }
    }

    void CreateSection(string title, string[] items,LayoutType type =LayoutType.Vertical)
    {
        if (items == null || items.Length == 0) return;

        CreateTitle(title,out GameObject titleObject);
        titleObject.name = title;

        var sectionConfig = creditsStyleRoot?.creditsStyle?.section;
        float space =sectionConfig?.spacing ?? 5.0f;

        var padding = sectionConfig?.padding;
        RectOffset rectPadding = new RectOffset(
            padding?.left ?? 0,
            padding?.right ?? 0,
            padding?.top ?? 34,
            padding?.bottom ?? 0
        );
       
        switch (type)
        {
            case LayoutType.Horizion:
                HorizontalLayoutGroup hg = titleObject.AddComponent<HorizontalLayoutGroup>();
                hg.padding = rectPadding;
                hg.spacing = space;
                hg.childAlignment = TextAnchor.MiddleCenter;
                break;
            case LayoutType.Vertical:
                VerticalLayoutGroup vg = titleObject.AddComponent<VerticalLayoutGroup>();
                vg.padding = rectPadding;
                vg.spacing = space;
                vg.childAlignment = TextAnchor.MiddleCenter;
                break;
        }

        foreach (string item in items)
        {
            CreateTextItem(item, out GameObject textObject);
            textObject.transform.SetParent(titleObject.transform);
        }

    }


    void CreateAudioProductionSection(string title, AudioProduction audio)
    {
        CreateTitle(title, out GameObject titleObject);
        titleObject.name = title;

        var sectionConfig = creditsStyleRoot?.creditsStyle?.section;
        float space = sectionConfig?.spacing ?? 5.0f;
        var padding = sectionConfig?.padding;
        RectOffset rectPadding = new RectOffset(
            padding?.left ?? 0,
            padding?.right ?? 0,
            padding?.top ?? 34,
            padding?.bottom ?? 0
        );

        VerticalLayoutGroup vg = titleObject.AddComponent<VerticalLayoutGroup>();
        vg.padding = rectPadding;
        vg.spacing = space;
        vg.childAlignment = TextAnchor.MiddleCenter;

        if (audio.company != null)
        {
            CreateTextItem($"{audio.company.name}: {audio.company.members}", out GameObject companyObject);
            companyObject.name = audio.company.name;
            companyObject.transform.SetParent(titleObject.transform);
        }

        if (audio.music_director != null)
        {
            CreateTextItem($"{audio.music_director.name}: {audio.music_director.members}", out GameObject musicDirectorObject);
            musicDirectorObject.name = audio.music_director.name;
            musicDirectorObject.transform.SetParent(titleObject.transform);
        }

        if (audio.composer != null)
        {
            CreateTextItem($"{audio.composer.name}: {audio.composer.members}", out GameObject composerObject);
            composerObject.name = audio.composer.name;
            composerObject.transform.SetParent(titleObject.transform);
        }

        if (audio.sound_design != null)
        {
            CreateTextItem($"{audio.sound_design.name}: {audio.sound_design.members}", out GameObject soundDesignObject);
            soundDesignObject.name = audio.sound_design.name;
            soundDesignObject.transform.SetParent(titleObject.transform);
        }

        if (audio.mixing != null)
        {
            CreateTextItem($"{audio.mixing.name}: {audio.mixing.members}", out GameObject mixingObject);
            mixingObject.name = audio.mixing.name;
            mixingObject.transform.SetParent(titleObject.transform);
        }

        if (audio.sound_producer != null)
        {
            CreateTextItem($"{audio.sound_producer.name}: {audio.sound_producer.members}", out GameObject soundProducerObject);
            soundProducerObject.name = audio.sound_producer.name;
            soundProducerObject.transform.SetParent(titleObject.transform);
        }

        if (audio.voice_director != null)
        {
            CreateTextItem($"{audio.voice_director.name}: {audio.voice_director.members}", out GameObject voiceDirectorObject);
            voiceDirectorObject.name = audio.voice_director.name;
            voiceDirectorObject.transform.SetParent(titleObject.transform);
        }

        if (audio.voice_actors != null)
        {
            CreateSubTitle(audio.voice_actors.name, out GameObject subTitleObject);

            VerticalLayoutGroup vg2 = subTitleObject.AddComponent<VerticalLayoutGroup>();
            vg2.padding = rectPadding;
            vg2.spacing = space;
            vg2.childAlignment = TextAnchor.MiddleCenter;
            subTitleObject.name = audio.voice_actors.name;
            subTitleObject.transform.SetParent(titleObject.transform);

            foreach (var voiceActor in audio.voice_actors.members)
            {
                CreateTextItem($"{voiceActor.character}: {voiceActor.actor}", out GameObject voiceObject);
                voiceObject.name = voiceActor.character;
                voiceObject.transform.SetParent(subTitleObject.transform);
            }
        }
    }


    void CreateTitle(string title ,out GameObject titleObject)
    {
        GameObject titleObj = CreateUIElement();
        titleObject = titleObj;
        TextMeshProUGUI titleText = titleObj.GetComponent<TextMeshProUGUI>();
        ContentSizeFitter fitter = titleObj.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var titleConfig = creditsStyleRoot?.creditsStyle?.title;
        titleText.text = title;
        titleText.alignment = TextAlignmentOptions.Top;
        titleText.fontSize = titleConfig?.fontSize ?? 24;
        titleText.fontStyle = GetFontStyle(titleConfig?.fontStyle ?? "Bold");
        titleText.color = GetColor(titleConfig?.fontColor ?? "#FFFFFF");
    }

    void CreateSubTitle(string subtitle,out GameObject subTitleObject)
    {
        GameObject subtitleObj = CreateUIElement();
        subTitleObject = subtitleObj;
        TextMeshProUGUI subtitleText = subtitleObj.GetComponent<TextMeshProUGUI>();
        
        var subtitleConfig = creditsStyleRoot?.creditsStyle?.subtitle;
        subtitleText.text = subtitle;
        subtitleText.fontSize = subtitleConfig?.fontSize ?? 18;
        subtitleText.fontStyle = GetFontStyle(subtitleConfig?.fontStyle ?? "Bold");
        subtitleText.color = GetColor(subtitleConfig?.fontColor ?? "#FFFFFF");
    }

    void CreateTextItem(string text,out GameObject textObject)
    {
        GameObject textObj = CreateUIElement();
        textObject = textObj;
        TextMeshProUGUI textComponent = textObj.GetComponent<TextMeshProUGUI>();

        ContentSizeFitter sizeFitter = textObj.AddComponent<ContentSizeFitter>();
        sizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var textConfig = creditsStyleRoot?.creditsStyle?.textItem;
        textComponent.text = text;
        textComponent.alignment = TextAlignmentOptions.Center;
        textComponent.fontStyle = GetFontStyle(textConfig?.fontStyle ?? "Normal");
        textComponent.fontSize = textConfig?.fontSize ?? 14;
        textComponent.color = GetColor(textConfig?.fontColor ?? "#FFFFFF");
    }

    GameObject CreateUIElement()
    {
        GameObject obj = new GameObject("CreditsText");
        obj.transform.SetParent(contentParent);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;
        obj.transform.localScale = Vector3.one;
        
        TextMeshProUGUI textComponent = obj.AddComponent<TextMeshProUGUI>();

        textComponent.alignment = TextAlignmentOptions.Center;
        textComponent.alignment = TextAlignmentOptions.Top;
        textComponent.font = fontAsset;


        return obj;
    }

    private FontStyles GetFontStyle(string styleString)
    {
        switch (styleString?.ToLower())
        {
            case "bold":
                return FontStyles.Bold;
            case "italic":
                return FontStyles.Italic;
            case "underline":
                return FontStyles.Underline;
            case "strikethrough":
                return FontStyles.Strikethrough;
            case "lowerCase":
                return FontStyles.LowerCase;
            case "upperCase":
                return FontStyles.UpperCase;
            case "smallCaps":
                return FontStyles.SmallCaps;
            case "superscript":
                return FontStyles.Superscript;
            case "subscript":
                return FontStyles.Subscript;
            case "highlight":
                return FontStyles.Highlight;
            default:
                return FontStyles.Normal;
        }
    }

    private Color GetColor(string colorString)
    {
        if (ColorUtility.TryParseHtmlString(colorString, out Color color))
        {
            return color;
        }
        return Color.white;
    }
}
