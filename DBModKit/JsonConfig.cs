using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using Newtonsoft.Json;
using UnityEngine;

namespace DBModKit
{
    /// <summary>
    /// 存在 BepInEx/config 下的 JSON 配置。修改后调用 <see cref="MarkDirty"/>，
    /// ModKit 会在一秒后自动保存（拖动滑条这类连续修改不会每帧写盘），游戏退出时也会保存。
    /// </summary>
    public sealed class JsonConfig<T> : IJsonConfig, IDisposable where T : new()
    {
        private bool dirty;
        private float lastSave;

        public string Path { get; }
        public T Data { get; private set; }

        /// <param name="fileName">文件名，例如 "SmartSkill.rules.json"；可以带子目录，保存时自动创建</param>
        public JsonConfig(string fileName)
        {
            Path = System.IO.Path.Combine(Paths.ConfigPath, fileName);
            Data = Read();
            JsonConfigRegistry.Add(this);
        }

        private T Read()
        {
            if (!File.Exists(Path)) return new T();
            try
            {
                return JsonConvert.DeserializeObject<T>(File.ReadAllText(Path)) ?? new T();
            }
            catch (Exception e)
            {
                // 保留损坏的文件，避免下次保存时把玩家的设置覆盖掉
                var backup = Path + ".broken";
                File.Copy(Path, backup, true);
                Log.Error($"读取 {Path} 失败，已备份为 {backup} 并使用默认值：{e.Message}");
                return new T();
            }
        }

        public void MarkDirty() => dirty = true;

        public void Flush()
        {
            if (!dirty) return;
            dirty = false;
            lastSave = Time.unscaledTime;
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                File.WriteAllText(Path, JsonConvert.SerializeObject(Data, Formatting.Indented));
            }
            catch (Exception e)
            {
                Log.Error($"保存 {Path} 失败：{e.Message}");
            }
        }

        /// <summary>保存并停止自动保存。用 ModContext.LoadJson 打开的配置会在卸载时自动调用。</summary>
        public void Dispose()
        {
            Flush();
            JsonConfigRegistry.Remove(this);
        }

        void IJsonConfig.Tick()
        {
            if (dirty && Time.unscaledTime - lastSave > 1f) Flush();
        }
    }

    internal interface IJsonConfig
    {
        void Tick();
        void Flush();
    }

    internal static class JsonConfigRegistry
    {
        private static readonly List<IJsonConfig> configs = new List<IJsonConfig>();

        public static void Add(IJsonConfig c) => configs.Add(c);

        public static void Remove(IJsonConfig c) => configs.Remove(c);

        public static void Tick()
        {
            foreach (var c in configs) c.Tick();
        }

        public static void FlushAll()
        {
            foreach (var c in configs) c.Flush();
        }
    }
}
