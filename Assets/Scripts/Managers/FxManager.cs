using UnityEngine;
using System.Collections.Generic;

public class FxManager : MonoBehaviour
{
    public static FxManager Instance { get; private set; }

    [System.Serializable]
    public class FxPool
    {
        public string fxName;
        public ParticleSystem fxPrefab;
        public int poolSize = 10;
        [Tooltip("Uniform size of this effect. Frequent effects (cash, harvest) read better small.")]
        public float scale = 1f;

        [HideInInspector]
        public List<ParticleSystem> poolList = new List<ParticleSystem>();
    }

    public List<FxPool> fxPools;

    private Dictionary<string, FxPool> poolDictionary = new Dictionary<string, FxPool>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        InitializePools();
    }

    private void InitializePools()
    {
        for (int i = 0; i < fxPools.Count; i++)
        {
            FxPool pool = fxPools[i];
            poolDictionary[pool.fxName] = pool;

            for (int j = 0; j < pool.poolSize; j++)
            {
                if (pool.fxPrefab != null)
                {
                    ParticleSystem obj = Instantiate(pool.fxPrefab, transform);
                    obj.gameObject.SetActive(false);
                    if (pool.scale > 0f && !Mathf.Approximately(pool.scale, 1f))
                    {
                        obj.transform.localScale = Vector3.one * pool.scale;
                        foreach (var ps in obj.GetComponentsInChildren<ParticleSystem>(true))
                        {
                            var psMain = ps.main;
                            psMain.scalingMode = ParticleSystemScalingMode.Hierarchy;
                        }
                    }

                    // Some library effects (leaves, confetti) are authored as looping emitters.
                    // A pooled one-shot must never loop, or it keeps playing forever once used.
                    foreach (var ps in obj.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        var m = ps.main;
                        m.loop = false;
                        m.playOnAwake = false;
                    }

                    var main = obj.main;
                    main.stopAction = ParticleSystemStopAction.Disable;

                    pool.poolList.Add(obj);
                }
            }
        }
    }

    private static float LongestLifetime(ParticleSystem root)
    {
        float t = 0f;
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var m = ps.main;
            t = Mathf.Max(t, m.startDelay.constantMax + m.duration + m.startLifetime.constantMax);
        }
        return Mathf.Min(t, 6f);
    }

    // Safety net: the pooled effect is always returned, even if a sub-emitter keeps it alive.
    private System.Collections.IEnumerator DisableAfter(ParticleSystem fx, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (fx != null && fx.gameObject.activeSelf)
        {
            fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            fx.gameObject.SetActive(false);
        }
    }

    public void PlayFx(string fxName, Vector3 position)
    {
        if (poolDictionary.TryGetValue(fxName, out FxPool pool))
        {
            for (int i = 0; i < pool.poolList.Count; i++)
            {
                ParticleSystem fx = pool.poolList[i];
                if (fx != null && !fx.gameObject.activeInHierarchy)
                {
                    fx.transform.position = position;
                    fx.gameObject.SetActive(true);
                    fx.Clear(true);
                    fx.Play(true);
                    StartCoroutine(DisableAfter(fx, LongestLifetime(fx) + 0.2f));
                    return;
                }
            }
        }
    }
}
