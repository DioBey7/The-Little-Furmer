using System.Collections.Generic;
using UnityEngine;

public class StockManager : MonoBehaviour
{
    public static StockManager Instance { get; private set; }

    [HideInInspector] public List<StockArea> activeStockAreas = new List<StockArea>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void InitializeStocks(List<StockArea> stocks)
    {
        activeStockAreas.Clear();
        if (stocks != null)
        {
            for (int i = 0; i < stocks.Count; i++)
            {
                activeStockAreas.Add(stocks[i]);
            }
        }
    }
}
