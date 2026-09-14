using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
#if (IncludeSetting)
using Colossal.IO.AssetDatabase;
#if (IncludeKeyBindings)
using Game.Input;
using UnityEngine;
#endif
#endif

namespace ModTemplate
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(ModTemplate)}.{nameof(Mod)}").SetShowsErrorsInUI(false);
#if (IncludeSetting)
        private Setting m_Setting;
#if (IncludeKeyBindings)
        public static ProxyAction m_ButtonAction;
        public static ProxyAction m_AxisAction;
        public static ProxyAction m_VectorAction;   

        public const string kButtonActionName = "ButtonBinding";
        public const string kAxisActionName = "FloatBinding";
        public const string kVectorActionName = "Vector2Binding";
#endif
#endif

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Current mod asset at {asset.path}");

#if (IncludeSetting)
            m_Setting = new Setting(this);
            m_Setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(m_Setting));

#if (IncludeKeyBindings)
            m_Setting.RegisterKeyBindings();

            m_ButtonAction = m_Setting.GetAction(kButtonActionName);
            m_AxisAction = m_Setting.GetAction(kAxisActionName);
            m_VectorAction = m_Setting.GetAction(kVectorActionName);
            
            m_ButtonAction.shouldBeEnabled = true;
            m_AxisAction.shouldBeEnabled = true;
            m_VectorAction.shouldBeEnabled = true;
            
            m_ButtonAction.onInteraction += (_, phase) => log.Info($"[{m_ButtonAction.name}] On{phase} {m_ButtonAction.ReadValue<float>()}");
            m_AxisAction.onInteraction += (_, phase) => log.Info($"[{m_AxisAction.name}] On{phase} {m_AxisAction.ReadValue<float>()}");
            m_VectorAction.onInteraction += (_, phase) => log.Info($"[{m_VectorAction.name}] On{phase} {m_VectorAction.ReadValue<Vector2>()}");
#endif

            AssetDatabase.global.LoadSettings(nameof(ModTemplate), m_Setting, new Setting(this));
#endif
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
#if (IncludeSetting)
            if (m_Setting != null)
            {
                m_Setting.UnregisterInOptionsUI();
                m_Setting = null;
            }
#endif
        }
    }
}
