﻿using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using Mono.Cecil;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ScriptEngine
{
    [BepInPlugin(GUID, "Script Engine", Version)]
    public class ScriptEngine : BaseUnityPlugin
    {
        public const string GUID = "com.bepis.bepinex.scriptengine.bepin5";
        public const string Version = "1.0.0";

        public string ScriptDirectory => Path.Combine(Paths.BepInExRootPath, "scripts");

        GameObject scriptManager;
        KeyCode reloadKey;

        ConfigWrapper<bool> LoadOnStart { get; set; }
        ConfigWrapper<string> ReloadKey { get; set; }

        void Awake()
        {
            LoadOnStart = Config.Wrap("General", "LoadOnStart", "Load all plugins from the scripts folder when starting the application", false);
            ReloadKey = Config.Wrap("General", "ReloadKey", "Press this key to reload all the plugins from the scripts folder", KeyCode.F6.ToString());
            reloadKey = (KeyCode)Enum.Parse(typeof(KeyCode), ReloadKey.Value, true);

            if(LoadOnStart.Value)
                ReloadPlugins();
        }

        void Update()
        {
            if(Input.GetKeyDown(reloadKey))
                ReloadPlugins();
        }

        void ReloadPlugins()
        {
            Destroy(scriptManager);
            scriptManager = new GameObject($"ScriptEngine_{DateTime.Now.Ticks}");
            DontDestroyOnLoad(scriptManager);

            var files = Directory.GetFiles(ScriptDirectory, "*.dll");
            if(files.Length > 0)
            {
                foreach(string path in Directory.GetFiles(ScriptDirectory, "*.dll"))
                    LoadDLL(path, scriptManager);

                Logger.LogMessage("Reloaded script plugins!");
            }
            else
            {
                Logger.LogMessage("No plugins to reload");
            }
        }

        void LoadDLL(string path, GameObject obj)
        {
            var defaultResolver = new DefaultAssemblyResolver();
            defaultResolver.AddSearchDirectory(ScriptDirectory);
            defaultResolver.AddSearchDirectory(Paths.ManagedPath);
            defaultResolver.AddSearchDirectory(Paths.BepInExAssemblyDirectory);

            using(var dll = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { AssemblyResolver = defaultResolver }))
            {
                dll.Name.Name = $"{dll.Name.Name}-{DateTime.Now.Ticks}";

                using(var ms = new MemoryStream())
                {
                    dll.Write(ms);
                    var ass = Assembly.Load(ms.ToArray());

                    foreach(Type type in ass.GetTypes())
                    {
                        if(typeof(BaseUnityPlugin).IsAssignableFrom(type))
                        {
                            var metadata = MetadataHelper.GetMetadata(type);
                            var typeDefinition = dll.MainModule.Types.First(x => x.FullName == type.FullName);
                            var typeInfo = Chainloader.ToPluginInfo(typeDefinition);
                            Chainloader.PluginInfos[metadata.GUID] = typeInfo;

                            Logger.Log(LogLevel.Info, $"Reloading {metadata.GUID}");
                            StartCoroutine(DelayAction(() => obj.AddComponent(type)));
                        }
                    }
                }
            }
        }

        IEnumerator DelayAction(Action action)
        {
            yield return null;
            action();
        }
    }
}
