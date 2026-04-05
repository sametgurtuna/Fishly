using UnityEngine;

public enum Rarity
{
    Common,    
    Uncommon,  
    Rare,      
    Epic,      
    Legendary  
}

[CreateAssetMenu(fileName = "New Fish", menuName = "Fish Data")]
public class FishData : ScriptableObject
{
    [Header("Visuals")]
    public string fishName;
    public Sprite fishSprite;

    [Header("Rarity")]
    public Rarity fishRarity; 

    [Header("Economy")]
    public double baseCost;
    public double baseIncome;
    
    [Header("Growth")]
    public float priceMultiplier = 1.15f;

    public Color GetRarityColor()
    {
        switch (fishRarity)
        {
            case Rarity.Common: return Color.white;
            case Rarity.Uncommon: return Color.green;
            case Rarity.Rare: return Color.blue;
            case Rarity.Epic: return new Color(0.5f, 0, 0.5f); 
            case Rarity.Legendary: return new Color(1, 0.5f, 0); 
            default: return Color.white;
        }
    }
}