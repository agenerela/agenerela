namespace GreyBoxStrategy
{
    /// <summary>
    /// The <b>Classic</b> preset: today's 25-province board as map data, the control for every A/B
    /// run (rules v2, section 2.6). Its rules fields are exactly what <c>MapDefinition.Standard()</c>
    /// built in code until R0; a test keeps that code and compares every field.
    /// </summary>
    /// <remarks>
    /// The data sits in a C# string rather than a file because the simulation has no UnityEngine and
    /// so cannot load an asset; it is read by the same <see cref="WorldMap.FromJson"/> every map file
    /// goes through. Terrain, coasts, ports and population are new in R0 and read by no rule yet:
    /// terrain was set by hand from the painting (mountains in the north-west, on the east coast and
    /// south of Harrow; the marsh by Thornbury; forests and fields elsewhere), every edge province is
    /// coastal, five spread-out coastal provinces have a port, and population follows v2 section 4.1.
    /// </remarks>
    public static class ClassicMap
    {
        public const string Name = "Classic";

        /// <summary>A fresh copy of the Classic map's data.</summary>
        public static WorldMap Load()
        {
            var map = WorldMap.FromJson(Json);
            foreach (var p in map.Provinces) p.Development = Development.Of(p.Population, p.Capital, p.Port);
            return map;
        }

        public const string Json = @"{
  ""format"": 1,
  ""name"": ""Classic"",
  ""countries"": [
    {
      ""id"": ""valen"",
      ""name"": ""Valen"",
      ""personality"": ""Proud and quick to take offence; believes strength earns respect."",
      ""startGold"": 40,
      ""startArmy"": 30
    },
    {
      ""id"": ""orsk"",
      ""name"": ""Orsk"",
      ""personality"": ""A merchant republic; prefers coin to blood, never forgets a betrayal."",
      ""startGold"": 40,
      ""startArmy"": 30
    },
    {
      ""id"": ""merrow"",
      ""name"": ""Merrow"",
      ""personality"": ""Small and devout; defends what it has and trusts no one."",
      ""startGold"": 40,
      ""startArmy"": 30
    },
    {
      ""id"": ""kesh"",
      ""name"": ""Kesh"",
      ""personality"": ""Hill clans under one banner; fierce in war, quick to make peace when it pays."",
      ""startGold"": 40,
      ""startArmy"": 30
    },
    {
      ""id"": ""ilvar"",
      ""name"": ""Ilvar"",
      ""personality"": ""Ambitious and patient; strikes whoever is weakest."",
      ""startGold"": 40,
      ""startArmy"": 30
    }
  ],
  ""provinces"": [
    {
      ""id"": ""highmoor"",
      ""name"": ""Highmoor"",
      ""column"": 0,
      ""row"": 0,
      ""owner"": ""valen"",
      ""capital"": true,
      ""forts"": 1,
      ""terrain"": ""mountains"",
      ""coastal"": true,
      ""port"": false,
      ""population"": 5
    },
    {
      ""id"": ""ashby"",
      ""name"": ""Ashby"",
      ""column"": 1,
      ""row"": 0,
      ""owner"": ""valen"",
      ""capital"": false,
      ""forts"": 0,
      ""terrain"": ""hills"",
      ""coastal"": true,
      ""port"": false,
      ""population"": 4
    },
    {
      ""id"": ""stonereach"",
      ""name"": ""Stonereach"",
      ""column"": 2,
      ""row"": 0,
      ""capital"": false,
      ""forts"": 0,
      ""terrain"": ""forest"",
      ""coastal"": true,
      ""port"": true,
      ""population"": 4
    },
    {
      ""id"": ""corwick"",
      ""name"": ""Corwick"",
      ""column"": 3,
      ""row"": 0,
      ""owner"": ""orsk"",
      ""capital"": false,
      ""forts"": 0,
      ""terrain"": ""plains"",
      ""coastal"": true,
      ""port"": false,
      ""population"": 5
    },
    {
      ""id"": ""orskhaven"",
      ""name"": ""Orskhaven"",
      ""column"": 4,
      ""row"": 0,
      ""owner"": ""orsk"",
      ""capital"": true,
      ""forts"": 1,
      ""terrain"": ""hills"",
      ""coastal"": true,
      ""port"": true,
      ""population"": 6
    },
    {
      ""id"": ""brindle"",
      ""name"": ""Brindle"",
      ""column"": 0,
      ""row"": 1,
      ""owner"": ""valen"",
      ""capital"": false,
      ""forts"": 0,
      ""terrain"": ""forest"",
      ""coastal"": true,
      ""port"": false,
      ""population"": 4
    },
    {
      ""id"": ""fenmark"",
      ""name"": ""Fenmark"",
      ""column"": 1,
      ""row"": 1,
      ""capital"": false,
      ""forts"": 0,
      ""terrain"": ""plains"",
      ""coastal"": false,
      ""port"": false,
      ""population"": 4
    },
    {
      ""id"": ""templegate"",
      ""name"": ""Templegate"",
      ""column"": 2,
      ""row"": 1,
      ""owner"": ""merrow"",
      ""capital"": false,
      ""forts"": 0,
      ""terrain"": ""forest"",
      ""coastal"": false,
      ""port"": false,
      ""population"": 3
    },
    {
      ""id"": ""saltmere"",
      ""name"": ""Saltmere"",
      ""column"": 3,
      ""row"": 1,
      ""capital"": false,
      ""forts"": 0,
      ""terrain"": ""plains"",
      ""coastal"": false,
      ""port"": false,
      ""population"": 4
    },
    {
      ""id"": ""redcliff"",
      ""name"": ""Redcliff"",
      ""column"": 4,
      ""row"": 1,
      ""owner"": ""orsk"",
      ""capital"": false,
      ""forts"": 0,
      ""terrain"": ""mountains"",
      ""coastal"": true,
      ""port"": false,
      ""population"": 3
    },
    {
      ""id"": ""westmarch"",
      ""name"": ""Westmarch"",
      ""column"": 0,
      ""row"": 2,
      ""capital"": false,
      ""forts"": 0,
      ""terrain"": ""plains"",
      ""coastal"": true,
      ""port"": true,
      ""population"": 5
    },
    {
      ""id"": ""harrow"",
      ""name"": ""Harrow"",
      ""column"": 1,
      ""row"": 2,
      ""capital"": false,
      ""forts"": 0,
      ""terrain"": ""forest"",
      ""coastal"": false,
      ""port"": false,
      ""population"": 3
    },
    {
      ""id"": ""merrowdeep"",
      ""name"": ""Merrowdeep"",
      ""column"": 2,
      ""row"": 2,
      ""owner"": ""merrow"",
      ""capital"": true,
      ""forts"": 1,
      ""terrain"": ""plains"",
      ""coastal"": false,
      ""port"": false,
      ""population"": 6
    },
    {
      ""id"": ""glimmerford"",
      ""name"": ""Glimmerford"",
      ""column"": 3,
      ""row"": 2,
      ""capital"": false,
      ""forts"": 0,
      ""terrain"": ""hills"",
      ""coastal"": false,
      ""port"": false,
      ""population"": 3
    },
    {
      ""id"": ""eastwatch"",
      ""name"": ""Eastwatch"",
      ""column"": 4,
      ""row"": 2,
      ""capital"": false,
      ""forts"": 0,
      ""terrain"": ""forest"",
      ""coastal"": true,
      ""port"": true,
      ""population"": 4
    },
    {
      ""id"": ""thornbury"",
      ""name"": ""Thornbury"",
      ""column"": 0,
      ""row"": 3,
      ""owner"": ""kesh"",
      ""capital"": false,
      ""forts"": 0,
      ""terrain"": ""marsh"",
      ""coastal"": true,
      ""port"": false,
      ""population"": 3
    },
    {
      ""id"": ""ravenholt"",
      ""name"": ""Ravenholt"",
      ""column"": 1,
      ""row"": 3,
      ""capital"": false,
      ""forts"": 0,
      ""terrain"": ""mountains"",
      ""coastal"": false,
      ""port"": false,
      ""population"": 2
    },
    {
      ""id"": ""pilgrimsrest"",
      ""name"": ""Pilgrim's Rest"",
      ""column"": 2,
      ""row"": 3,
      ""owner"": ""merrow"",
      ""capital"": false,
      ""forts"": 0,
      ""terrain"": ""plains"",
      ""coastal"": false,
      ""port"": false,
      ""population"": 4
    },
    {
      ""id"": ""duskvale"",
      ""name"": ""Duskvale"",
      ""column"": 3,
      ""row"": 3,
      ""capital"": false,
      ""forts"": 0,
      ""terrain"": ""forest"",
      ""coastal"": false,
      ""port"": false,
      ""population"": 3
    },
    {
      ""id"": ""ilmoor"",
      ""name"": ""Ilmoor"",
      ""column"": 4,
      ""row"": 3,
      ""owner"": ""ilvar"",
      ""capital"": false,
      ""forts"": 0,
      ""terrain"": ""plains"",
      ""coastal"": true,
      ""port"": false,
      ""population"": 5
    },
    {
      ""id"": ""keshtar"",
      ""name"": ""Keshtar"",
      ""column"": 0,
      ""row"": 4,
      ""owner"": ""kesh"",
      ""capital"": true,
      ""forts"": 1,
      ""terrain"": ""hills"",
      ""coastal"": true,
      ""port"": false,
      ""population"": 6
    },
    {
      ""id"": ""marrowfield"",
      ""name"": ""Marrowfield"",
      ""column"": 1,
      ""row"": 4,
      ""owner"": ""kesh"",
      ""capital"": false,
      ""forts"": 0,
      ""terrain"": ""plains"",
      ""coastal"": true,
      ""port"": false,
      ""population"": 5
    },
    {
      ""id"": ""southgate"",
      ""name"": ""Southgate"",
      ""column"": 2,
      ""row"": 4,
      ""capital"": false,
      ""forts"": 0,
      ""terrain"": ""plains"",
      ""coastal"": true,
      ""port"": true,
      ""population"": 5
    },
    {
      ""id"": ""oakhollow"",
      ""name"": ""Oakhollow"",
      ""column"": 3,
      ""row"": 4,
      ""owner"": ""ilvar"",
      ""capital"": false,
      ""forts"": 0,
      ""terrain"": ""forest"",
      ""coastal"": true,
      ""port"": false,
      ""population"": 4
    },
    {
      ""id"": ""ilvarion"",
      ""name"": ""Ilvarion"",
      ""column"": 4,
      ""row"": 4,
      ""owner"": ""ilvar"",
      ""capital"": true,
      ""forts"": 1,
      ""terrain"": ""plains"",
      ""coastal"": true,
      ""port"": false,
      ""population"": 7
    }
  ],
  ""borders"": [
    [
      ""highmoor"",
      ""ashby""
    ],
    [
      ""highmoor"",
      ""brindle""
    ],
    [
      ""ashby"",
      ""stonereach""
    ],
    [
      ""ashby"",
      ""fenmark""
    ],
    [
      ""stonereach"",
      ""corwick""
    ],
    [
      ""stonereach"",
      ""templegate""
    ],
    [
      ""corwick"",
      ""orskhaven""
    ],
    [
      ""corwick"",
      ""saltmere""
    ],
    [
      ""orskhaven"",
      ""redcliff""
    ],
    [
      ""brindle"",
      ""fenmark""
    ],
    [
      ""brindle"",
      ""westmarch""
    ],
    [
      ""fenmark"",
      ""templegate""
    ],
    [
      ""fenmark"",
      ""harrow""
    ],
    [
      ""templegate"",
      ""saltmere""
    ],
    [
      ""templegate"",
      ""merrowdeep""
    ],
    [
      ""saltmere"",
      ""redcliff""
    ],
    [
      ""saltmere"",
      ""glimmerford""
    ],
    [
      ""redcliff"",
      ""eastwatch""
    ],
    [
      ""westmarch"",
      ""harrow""
    ],
    [
      ""westmarch"",
      ""thornbury""
    ],
    [
      ""harrow"",
      ""merrowdeep""
    ],
    [
      ""harrow"",
      ""ravenholt""
    ],
    [
      ""merrowdeep"",
      ""glimmerford""
    ],
    [
      ""merrowdeep"",
      ""pilgrimsrest""
    ],
    [
      ""glimmerford"",
      ""eastwatch""
    ],
    [
      ""glimmerford"",
      ""duskvale""
    ],
    [
      ""eastwatch"",
      ""ilmoor""
    ],
    [
      ""thornbury"",
      ""ravenholt""
    ],
    [
      ""thornbury"",
      ""keshtar""
    ],
    [
      ""ravenholt"",
      ""pilgrimsrest""
    ],
    [
      ""ravenholt"",
      ""marrowfield""
    ],
    [
      ""pilgrimsrest"",
      ""duskvale""
    ],
    [
      ""pilgrimsrest"",
      ""southgate""
    ],
    [
      ""duskvale"",
      ""ilmoor""
    ],
    [
      ""duskvale"",
      ""oakhollow""
    ],
    [
      ""ilmoor"",
      ""ilvarion""
    ],
    [
      ""keshtar"",
      ""marrowfield""
    ],
    [
      ""marrowfield"",
      ""southgate""
    ],
    [
      ""southgate"",
      ""oakhollow""
    ],
    [
      ""oakhollow"",
      ""ilvarion""
    ]
  ],
  ""regions"": [],
  ""specials"": []
}";
    }
}
