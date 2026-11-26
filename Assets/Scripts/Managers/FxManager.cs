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

                    var main = obj.main;
                    main.stopAction = ParticleSystemStopAction.Disable;

                    pool.poolList.Add(obj);
                }
            }
        }
    }

    public void PlayFx(string fxName, Vector3 position)
    {
        if (poolDictionary.TryGetValue(fxName, out FxPool pool))
        {
            for (int i = 0; i < pool.poolList.Count; i++)
            {
                ParticleSystem fx = pool.poolList[i];
                if (!fx.gameObject.activeInHierarchy)
                {
                    fx.transform.position = position;
                    fx.gameObject.SetActive(true);
                    fx.Play();
                    return;
                }
            }
        }
    }
}
