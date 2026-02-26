using System;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.SceneManagement;

/// <summary>
/// Global registry + state store for Anamorphic reveal amounts.
/// - Lives across scenes (DontDestroyOnLoad)
/// - Followers register via AnamorphicTailAlphaDriver
/// - Gameplay/VN can set/ramp reveal using stable keys: drawingKey + followerKey (+ optional instanceTag)
/// - Supports Reveal Groups via follower groupKey
/// </summary>
public class AnamorphicRevealDirector : MonoBehaviour
{
    // -------------------------------
    // Singleton
    // -------------------------------
    public static AnamorphicRevealDirector Instance { get; private set; }

    [Header("Behavior")]
    [Tooltip("If true, this object persists across scene loads.")]
    public bool dontDestroyOnLoad = true;

    [Tooltip("If a follower asks for Global reveal but no channel exists yet, start from this value.")]
    [Range(0f, 1f)]
    public float defaultReveal = 0f;

    [Header("Robustness")]
    [Tooltip("If true, the director scans the scene for followers on startup and after scene loads.")]
    public bool autoScanFollowers = true;

    [Tooltip("If a group is requested but not found, the director will rebuild groups from live followers and retry once.")]
    public bool rebuildGroupsOnMiss = true;

    // -------------------------------
    // Channel Model
    // -------------------------------
    [Serializable]
    private class RevealChannel
    {
        public float current = 0f;
        public float target = 0f;
        public float rampSeconds = 0f;
        public float rampElapsed = 0f;
        public bool locked = false;
    }

    private readonly struct ChannelKey : IEquatable<ChannelKey>
    {
        public readonly string drawingKey;
        public readonly string followerKey;
        public readonly string instanceTag; // optional

        public ChannelKey(string drawingKey, string followerKey, string instanceTag)
        {
            this.drawingKey = drawingKey ?? "";
            this.followerKey = followerKey ?? "";
            this.instanceTag = instanceTag ?? "";
        }

        public bool Equals(ChannelKey other)
        {
            return drawingKey == other.drawingKey &&
                   followerKey == other.followerKey &&
                   instanceTag == other.instanceTag;
        }

        public override bool Equals(object obj) => obj is ChannelKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = drawingKey.GetHashCode();
                hash = (hash * 397) ^ followerKey.GetHashCode();
                hash = (hash * 397) ^ instanceTag.GetHashCode();
                return hash;
            }
        }

        public override string ToString() => $"({drawingKey}/{followerKey}/{instanceTag})";
    }

    private readonly struct GroupKey : IEquatable<GroupKey>
    {
        public readonly string drawingKey;
        public readonly string groupKey;
        public readonly string instanceTag; // optional

        public GroupKey(string drawingKey, string groupKey, string instanceTag)
        {
            this.drawingKey = drawingKey ?? "";
            this.groupKey = groupKey ?? "";
            this.instanceTag = instanceTag ?? "";
        }

        public bool Equals(GroupKey other)
        {
            return drawingKey == other.drawingKey &&
                   groupKey == other.groupKey &&
                   instanceTag == other.instanceTag;
        }

        public override bool Equals(object obj) => obj is GroupKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = drawingKey.GetHashCode();
                hash = (hash * 397) ^ groupKey.GetHashCode();
                hash = (hash * 397) ^ instanceTag.GetHashCode();
                return hash;
            }
        }

        public override string ToString() => $"({drawingKey}::{groupKey}::{instanceTag})";
    }

    // -------------------------------
    // Storage
    // -------------------------------
    private readonly Dictionary<ChannelKey, RevealChannel> _channels = new Dictionary<ChannelKey, RevealChannel>(256);

    // Track live followers so we can rebuild groups.
    private readonly HashSet<AnamorphicTailAlphaDriver> _followers = new HashSet<AnamorphicTailAlphaDriver>();

    // Reveal groups: group -> followerKeys (resolved)
    private readonly Dictionary<GroupKey, HashSet<string>> _groups = new Dictionary<GroupKey, HashSet<string>>(128);

    // -------------------------------
    // Unity lifecycle
    // -------------------------------
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;

        if (autoScanFollowers)
            ScanAndRegisterAllFollowers();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!autoScanFollowers) return;
        ScanAndRegisterAllFollowers();
    }

    private void Update()
    {
        if (_channels.Count == 0) return;

        float dt = Time.deltaTime;

        // Advance ramps (RevealChannel is a class, so edits persist)
        foreach (var kv in _channels)
        {
            RevealChannel c = kv.Value;
            if (c.locked) continue;

            if (c.rampSeconds > 0f && c.current != c.target)
            {
                c.rampElapsed += dt;
                float t = Mathf.Clamp01(c.rampElapsed / Mathf.Max(0.0001f, c.rampSeconds));

                // Lerp from current to target each frame using t (works fine for ramps, and snaps near end)
                c.current = Mathf.Lerp(c.current, c.target, t);

                if (t >= 1f || Mathf.Abs(c.current - c.target) < 0.0005f)
                {
                    c.current = c.target;
                    c.rampSeconds = 0f;
                    c.rampElapsed = 0f;
                }
            }
        }
    }

    // -------------------------------
    // Public API (Gameplay/VN)
    // -------------------------------
    public void SetReveal(string drawingKey, string followerKey, float amount, string instanceTag = "")
    {
        var key = new ChannelKey(Norm(drawingKey), Norm(followerKey), Norm(instanceTag));
        RevealChannel c = GetOrCreateChannel(key);

        if (c.locked) return;

        c.current = Mathf.Clamp01(amount);
        c.target = c.current;
        c.rampSeconds = 0f;
        c.rampElapsed = 0f;
    }

    public void RampReveal(string drawingKey, string followerKey, float targetAmount, float seconds, string instanceTag = "")
    {
        var key = new ChannelKey(Norm(drawingKey), Norm(followerKey), Norm(instanceTag));
        RevealChannel c = GetOrCreateChannel(key);

        if (c.locked) return;

        c.target = Mathf.Clamp01(targetAmount);
        c.rampSeconds = Mathf.Max(0f, seconds);
        c.rampElapsed = 0f;

        if (c.rampSeconds <= 0.0001f)
        {
            c.current = c.target;
            c.rampSeconds = 0f;
        }
    }

    public float GetReveal(string drawingKey, string followerKey, string instanceTag = "")
    {
        var key = new ChannelKey(Norm(drawingKey), Norm(followerKey), Norm(instanceTag));
        if (_channels.TryGetValue(key, out RevealChannel c))
            return Mathf.Clamp01(c.current);

        return defaultReveal;
    }

    public void LockReveal(string drawingKey, string followerKey, bool locked, string instanceTag = "")
    {
        var key = new ChannelKey(Norm(drawingKey), Norm(followerKey), Norm(instanceTag));
        RevealChannel c = GetOrCreateChannel(key);
        c.locked = locked;
    }

    // -------------------------------
    // Reveal Groups API
    // -------------------------------
    public void SetRevealGroup(string drawingKey, string groupKey, float amount, string instanceTag = "")
    {
        var gk = new GroupKey(Norm(drawingKey), Norm(groupKey), Norm(instanceTag));

        if (!TryGetGroupFollowers(gk, out var followerKeys))
        {
            Debug.LogWarning($"SetRevealGroup: group not found or empty: {gk}");
            return;
        }

        foreach (var fk in followerKeys)
            SetReveal(drawingKey, fk, amount, instanceTag);
    }

    public void RampRevealGroup(string drawingKey, string groupKey, float targetAmount, float seconds, string instanceTag = "")
    {
        var gk = new GroupKey(Norm(drawingKey), Norm(groupKey), Norm(instanceTag));

        if (!TryGetGroupFollowers(gk, out var followerKeys))
        {
            Debug.LogWarning($"RampRevealGroup: group not found or empty: {gk}");
            return;
        }

        foreach (var fk in followerKeys)
            RampReveal(drawingKey, fk, targetAmount, seconds, instanceTag);
    }

    private bool TryGetGroupFollowers(GroupKey gk, out HashSet<string> followerKeys)
    {
        if (_groups.TryGetValue(gk, out followerKeys) && followerKeys != null && followerKeys.Count > 0)
            return true;

        if (!rebuildGroupsOnMiss)
        {
            followerKeys = null;
            return false;
        }

        // Rebuild groups from whatever followers we know about
        RebuildGroupsFromFollowers();

        if (_groups.TryGetValue(gk, out followerKeys) && followerKeys != null && followerKeys.Count > 0)
            return true;

        followerKeys = null;
        return false;
    }

    // -------------------------------
    // Called by TailAlphaDriver (safe static helpers)
    // -------------------------------
    public static void TryRegisterFollower(AnamorphicTailAlphaDriver driver)
    {
        if (driver == null) return;
        if (Instance == null) return;

        Instance.RegisterFollowerInternal(driver);
    }

    public static void TryUnregisterFollower(AnamorphicTailAlphaDriver driver)
    {
        if (driver == null) return;
        if (Instance == null) return;

        Instance.UnregisterFollowerInternal(driver);
    }

    public static float GetRevealOrFallback(AnamorphicTailAlphaDriver driver, float fallback)
    {
        if (driver == null) return fallback;
        if (Instance == null) return fallback;

        if (!driver.TryGetIdentity(out string drawingKey, out string followerKey, out string instanceTag))
            return fallback;

        float v = Instance.GetReveal(drawingKey, followerKey, instanceTag);

        if (!string.IsNullOrWhiteSpace(instanceTag))
        {
            var taggedKey = new ChannelKey(Norm(drawingKey), Norm(followerKey), Norm(instanceTag));
            if (!Instance._channels.ContainsKey(taggedKey))
                v = Instance.GetReveal(drawingKey, followerKey, "");
        }

        return v;
    }

    // -------------------------------
    // Internals
    // -------------------------------
    private void RegisterFollowerInternal(AnamorphicTailAlphaDriver driver)
    {
        _followers.Add(driver);

        if (driver.TryGetIdentity(out string drawingKey, out string followerKey, out string instanceTag))
        {
            var ckey = new ChannelKey(Norm(drawingKey), Norm(followerKey), Norm(instanceTag));
            GetOrCreateChannel(ckey);

            TryRegisterFollowerGroup(driver, drawingKey, instanceTag);
        }
    }

    private void UnregisterFollowerInternal(AnamorphicTailAlphaDriver driver)
    {
        _followers.Remove(driver);
        TryUnregisterFollowerGroup(driver);
    }

    private RevealChannel GetOrCreateChannel(ChannelKey key)
    {
        if (_channels.TryGetValue(key, out RevealChannel c))
            return c;

        c = new RevealChannel
        {
            current = Mathf.Clamp01(defaultReveal),
            target = Mathf.Clamp01(defaultReveal),
            rampSeconds = 0f,
            rampElapsed = 0f,
            locked = false
        };

        _channels[key] = c;
        return c;
    }

    private void TryRegisterFollowerGroup(AnamorphicTailAlphaDriver driver, string drawingKey, string instanceTag)
    {
        if (driver == null) return;

        var follow = driver.GetComponent<AnamorphicFollowStroke>();
        if (follow == null) return;

        string group = follow.ResolvedGroupKey;
        if (string.IsNullOrWhiteSpace(group)) return;

        string followerKey = follow.ResolvedFollowerKey;
        if (string.IsNullOrWhiteSpace(followerKey)) return;

        var gk = new GroupKey(Norm(drawingKey), Norm(group), Norm(instanceTag));
        if (!_groups.TryGetValue(gk, out HashSet<string> set))
        {
            set = new HashSet<string>();
            _groups[gk] = set;
        }

        set.Add(Norm(followerKey));
    }

    private void TryUnregisterFollowerGroup(AnamorphicTailAlphaDriver driver)
    {
        if (driver == null) return;

        if (!driver.TryGetIdentity(out string drawingKey, out string followerKey, out string instanceTag))
            return;

        var follow = driver.GetComponent<AnamorphicFollowStroke>();
        if (follow == null) return;

        string group = follow.ResolvedGroupKey;
        if (string.IsNullOrWhiteSpace(group)) return;

        var gk = new GroupKey(Norm(drawingKey), Norm(group), Norm(instanceTag));
        if (_groups.TryGetValue(gk, out HashSet<string> set))
        {
            set.Remove(Norm(followerKey));
            if (set.Count == 0) _groups.Remove(gk);
        }
    }

    private void ScanAndRegisterAllFollowers()
    {
        // Best-effort: clear and rebuild groups, but keep channels (state) intact.
        // Followers may already be in _followers; we refresh membership.
        var drivers = FindObjectsByType<AnamorphicTailAlphaDriver>(FindObjectsSortMode.None);

        for (int i = 0; i < drivers.Length; i++)
        {
            if (drivers[i] == null) continue;
            RegisterFollowerInternal(drivers[i]);
        }

        RebuildGroupsFromFollowers();
    }

    private void RebuildGroupsFromFollowers()
    {
        _groups.Clear();

        foreach (var driver in _followers)
        {
            if (driver == null) continue;

            if (driver.TryGetIdentity(out string drawingKey, out string followerKey, out string instanceTag))
            {
                // Ensure channel exists too (so Global reveal reads immediately)
                var ckey = new ChannelKey(Norm(drawingKey), Norm(followerKey), Norm(instanceTag));
                GetOrCreateChannel(ckey);

                TryRegisterFollowerGroup(driver, drawingKey, instanceTag);
            }
        }
    }

    private static string Norm(string s) => string.IsNullOrWhiteSpace(s) ? "" : s.Trim();

    // -------------------------------
    // DEBUG
    // -------------------------------
    [ContextMenu("DEBUG_PrintRevealState")]
    public void DEBUG_PrintRevealState()
    {
        Debug.Log("=== ANAMORPHIC REVEAL DEBUG START ===");

        Debug.Log($"Followers tracked: {_followers.Count}");
        Debug.Log($"Active Channels: {_channels.Count}");

        foreach (var kv in _channels)
        {
            var key = kv.Key;
            var channel = kv.Value;
            Debug.Log($"Channel: Drawing='{key.drawingKey}' | Follower='{key.followerKey}' | Instance='{key.instanceTag}' | Reveal={channel.current:F2}");
        }

        Debug.Log($"Active Groups: {_groups.Count}");
        foreach (var kv in _groups)
        {
            var gk = kv.Key;
            var followers = kv.Value;
            string followerList = string.Join(", ", followers);
            Debug.Log($"Group: Drawing='{gk.drawingKey}' | Group='{gk.groupKey}' | Instance='{gk.instanceTag}' | Followers=[{followerList}]");
        }

        Debug.Log("=== ANAMORPHIC REVEAL DEBUG END ===");
    }
}