using System;
using Newtonsoft.Json;

[Serializable]
public class MysteryData
{
    [JsonProperty("story")]
    public string Story;

    [JsonProperty("victim")]
    public string Victim;

    [JsonProperty("murder_weapon")]
    public string MurderWeapon;
}