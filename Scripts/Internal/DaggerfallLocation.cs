﻿// Project:         Daggerfall Tools For Unity
// Copyright:       Copyright (C) 2009-2015 Daggerfall Workshop
// Web Site:        http://www.dfworkshop.net
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Source Code:     https://github.com/Interkarma/daggerfall-unity
// Original Author: Gavin Clayton (interkarma@dfworkshop.net)
// Contributors:    
// 
// Notes:
//

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DaggerfallConnect;
using DaggerfallConnect.Utility;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop.Utility;

namespace DaggerfallWorkshop
{
    public class DaggerfallLocation : MonoBehaviour
    {
        bool isSet = false;
        DaggerfallUnity dfUnity;

        [SerializeField]
        private LocationSummary summary;

        // Climate texture swaps
        public LocationClimateUse ClimateUse = LocationClimateUse.UseLocation;
        public ClimateBases CurrentClimate = ClimateBases.Temperate;
        public ClimateSeason CurrentSeason = ClimateSeason.Summer;
        public ClimateNatureSets CurrentNatureSet = ClimateNatureSets.TemperateWoodland;

        // Window texture swaps
        public WindowStyle WindowTextureStyle = WindowStyle.Day;

        // Internal time and space texture swaps
        DaggerfallDateTime.Seasons lastSeason;
        bool lastCityLightsFlag;

        // Start markers
        [SerializeField]
        List<GameObject> startMarkers = new List<GameObject>();

        public LocationSummary Summary
        {
            get { return summary; }
        }

        public GameObject[] StartMarkers
        {
            get
            {
                if (startMarkers.Count == 0) EnumerateStartMarkers();
                return startMarkers.ToArray();
            }
        }

        [Serializable]
        public struct LocationSummary
        {
            public int ID;
            public int Longitude;
            public int Latitude;
            public int MapPixelX;
            public int MapPixelY;
            public int WorldCoordX;
            public int WorldCoordZ;
            public string RegionName;
            public string LocationName;
            public int WorldClimate;
            public DFRegion.LocationTypes LocationType;
            public DFRegion.DungeonTypes DungeonType;
            public bool HasDungeon;
            public ClimateBases Climate;
            public ClimateNatureSets Nature;
            public int SkyBase;
            public DaggerfallBillboardBatch NatureBillboardBatch;
        }

        void Start()
        {
        }

        void Update()
        {
            // Do nothing if not ready
            if (!ReadyCheck())
                return;

            // Handle automated texture swaps
            if (dfUnity.Option_AutomateTextureSwaps)
            {
                // Only process if climate, season, day/night, or weather changed
                if (lastSeason != dfUnity.WorldTime.Now.SeasonValue ||
                    lastCityLightsFlag != dfUnity.WorldTime.Now.IsCityLightsOn)
                {
                    ApplyTimeAndSpace();
                    lastSeason = dfUnity.WorldTime.Now.SeasonValue;
                    lastCityLightsFlag = dfUnity.WorldTime.Now.IsCityLightsOn;
                }
            }
        }

        private void ApplyTimeAndSpace()
        {
            // Get season and weather
            if (dfUnity.WorldTime.Now.SeasonValue == DaggerfallDateTime.Seasons.Winter)
                CurrentSeason = ClimateSeason.Winter;
            else
                CurrentSeason = ClimateSeason.Summer;

            // Set windows
            if (dfUnity.WorldTime.Now.IsCityLightsOn)
                WindowTextureStyle = WindowStyle.Night;
            else
                WindowTextureStyle = WindowStyle.Day;

            // Apply changes
            ApplyClimateSettings();
        }

        public void SetLocation(DFLocation location, bool performLayout = true)
        {
            if (!ReadyCheck())
                return;

            // Validate
            if (this.isSet)
                throw new Exception("This location has already been set.");
            if (!location.Loaded)
                throw new Exception("DFLocation not loaded.");

            // Set summary
            summary = new LocationSummary();
            summary.ID = location.MapTableData.MapId;
            summary.Longitude = (int)location.MapTableData.Longitude;
            summary.Latitude = (int)location.MapTableData.Latitude;
            DFPosition mapPixel = MapsFile.LongitudeLatitudeToMapPixel(summary.Longitude, summary.Latitude);
            DFPosition worldCoord = MapsFile.MapPixelToWorldCoord(mapPixel.X, mapPixel.Y);
            summary.MapPixelX = mapPixel.X;
            summary.MapPixelY = mapPixel.Y;
            summary.WorldCoordX = worldCoord.X;
            summary.WorldCoordZ = worldCoord.Y;
            summary.RegionName = location.RegionName;
            summary.LocationName = location.Name;
            summary.WorldClimate = location.Climate.WorldClimate;
            summary.LocationType = location.MapTableData.LocationType;
            summary.DungeonType = location.MapTableData.DungeonType;
            summary.HasDungeon = location.HasDungeon;
            summary.Climate = ClimateSwaps.FromAPIClimateBase(location.Climate.ClimateType);
            summary.Nature = ClimateSwaps.FromAPITextureSet(location.Climate.NatureSet);
            summary.SkyBase = location.Climate.SkyBase;

            // Assign starting climate
            CurrentSeason = ClimateSeason.Summer;
            CurrentClimate = summary.Climate;
            CurrentNatureSet = summary.Nature;

            // Perform layout
            if (performLayout)
            {
                LayoutLocation(ref location);
                ApplyClimateSettings();
            }

            // Seal location
            isSet = true;
        }

        public void ApplyClimateSettings()
        {
            // Do nothing if not ready
            if (!ReadyCheck())
                return;

            // Process all DaggerfallMesh child components
            DaggerfallMesh[] meshArray = GetComponentsInChildren<DaggerfallMesh>();
            foreach (var dm in meshArray)
            {
                switch (ClimateUse)
                {
                    case LocationClimateUse.UseLocation:
                        dm.SetClimate(Summary.Climate, CurrentSeason, WindowTextureStyle);
                        break;
                    case LocationClimateUse.Custom:
                        dm.SetClimate(CurrentClimate, CurrentSeason, WindowTextureStyle);
                        break;
                    case LocationClimateUse.Disabled:
                        dm.DisableClimate(dfUnity);
                        break;
                }
            }

            // Process all DaggerfallGroundMesh child components
            DaggerfallGroundPlane[] groundMeshArray = GetComponentsInChildren<DaggerfallGroundPlane>();
            foreach (var gm in groundMeshArray)
            {
                switch (ClimateUse)
                {
                    case LocationClimateUse.UseLocation:
                        gm.SetClimate(dfUnity, Summary.Climate, CurrentSeason);
                        break;
                    case LocationClimateUse.Custom:
                        gm.SetClimate(dfUnity, CurrentClimate, CurrentSeason);
                        break;
                    case LocationClimateUse.Disabled:
                        gm.SetClimate(dfUnity, ClimateBases.Temperate, ClimateSeason.Summer);
                        break;
                }
            }

            // Determine correct nature archive
            int natureArchive = GetNatureArchive();

            // Process all DaggerfallBillboard child components
            DaggerfallBillboard[] billboardArray = GetComponentsInChildren<DaggerfallBillboard>();
            foreach (var db in billboardArray)
            {
                if (db.Summary.FlatType == FlatTypes.Nature)
                {
                    // Apply recalculated nature archive
                    db.SetMaterial(natureArchive, db.Summary.Record, 0, db.Summary.InDungeon);
                }
                else
                {
                    // All other flats are just reapplied to handle any other changes
                    db.SetMaterial(db.Summary.Archive, db.Summary.Record, 0, db.Summary.InDungeon);
                }
            }

            // Process nature billboard batch
            if (summary.NatureBillboardBatch != null)
            {
                summary.NatureBillboardBatch.SetMaterial(natureArchive, true);
                summary.NatureBillboardBatch.Apply();
            }
        }

        /// <summary>
        /// Rebuild start markers.
        /// </summary>
        public void EnumerateStartMarkers()
        {
            // Process all DaggerfallBillboard child components
            DaggerfallBillboard[] billboardArray = GetComponentsInChildren<DaggerfallBillboard>();
            startMarkers.Clear();
            foreach (var db in billboardArray)
            {
                if (db.Summary.FlatType == FlatTypes.Editor && db.Summary.EditorFlatType == EditorFlatTypes.Start)
                {
                    startMarkers.Add(db.gameObject);
                }
            }
        }

        #region Private Methods

        private int GetNatureArchive()
        {
            int natureArchive;
            switch (ClimateUse)
            {
                case LocationClimateUse.UseLocation:
                    natureArchive = ClimateSwaps.GetNatureArchive(summary.Nature, CurrentSeason);
                    break;
                case LocationClimateUse.Custom:
                    natureArchive = ClimateSwaps.GetNatureArchive(CurrentNatureSet, CurrentSeason);
                    break;
                case LocationClimateUse.Disabled:
                default:
                    natureArchive = ClimateSwaps.GetNatureArchive(ClimateNatureSets.TemperateWoodland, ClimateSeason.Summer);
                    break;
            }

            return natureArchive;
        }

        /// <summary>
        /// Performs a fully standalone in-place location layout.
        /// This method is used only by editor layouts, not by streaming world.
        /// </summary>
        private void LayoutLocation(ref DFLocation location)
        {
            // Get city dimensions
            int width = location.Exterior.ExteriorData.Width;
            int height = location.Exterior.ExteriorData.Height;

            // Create billboard batch game objects for this location
            TextureAtlasBuilder miscBillboardAtlas = null;
            summary.NatureBillboardBatch = null;
            DaggerfallBillboardBatch lightsBillboardBatch = null;
            DaggerfallBillboardBatch animalsBillboardBatch = null;
            DaggerfallBillboardBatch miscBillboardBatch = null;
            if (dfUnity.Option_BatchBillboards)
            {
                miscBillboardAtlas = dfUnity.MaterialReader.MiscBillboardAtlas;
                int natureArchive = ClimateSwaps.GetNatureArchive(CurrentNatureSet, CurrentSeason);
                summary.NatureBillboardBatch = GameObjectHelper.CreateBillboardBatchGameObject(natureArchive, transform);
                lightsBillboardBatch = GameObjectHelper.CreateBillboardBatchGameObject(TextureReader.LightsTextureArchive, transform);
                animalsBillboardBatch = GameObjectHelper.CreateBillboardBatchGameObject(TextureReader.AnimalsTextureArchive, transform);
                miscBillboardBatch = GameObjectHelper.CreateBillboardBatchGameObject(miscBillboardAtlas.AtlasMaterial, transform);
            }

            // Import blocks
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (dfUnity.Option_BatchBillboards)
                    {
                        // Set block origin for billboard batches
                        // This causes next additions to be offset by this position
                        Vector3 blockOrigin = new Vector3((x * RMBLayout.RMBSide), 0, (y * RMBLayout.RMBSide));
                        summary.NatureBillboardBatch.BlockOrigin = blockOrigin;
                        lightsBillboardBatch.BlockOrigin = blockOrigin;
                        animalsBillboardBatch.BlockOrigin = blockOrigin;
                        miscBillboardBatch.BlockOrigin = blockOrigin;
                    }

                    string blockName = dfUnity.ContentReader.BlockFileReader.CheckName(dfUnity.ContentReader.MapFileReader.GetRmbBlockName(ref location, x, y));
                    GameObject go = GameObjectHelper.CreateRMBBlockGameObject(
                        blockName,
                        dfUnity.Option_RMBGroundPlane,
                        dfUnity.Option_CityBlockPrefab,
                        summary.NatureBillboardBatch,
                        lightsBillboardBatch,
                        animalsBillboardBatch,
                        miscBillboardAtlas,
                        miscBillboardBatch,
                        CurrentNatureSet,
                        CurrentSeason);
                    go.transform.parent = this.transform;
                    go.transform.position = new Vector3((x * RMBLayout.RMBSide), 0, (y * RMBLayout.RMBSide));
                }
            }

            // Apply batches
            if (summary.NatureBillboardBatch) summary.NatureBillboardBatch.Apply();
            if (lightsBillboardBatch) lightsBillboardBatch.Apply();
            if (animalsBillboardBatch) animalsBillboardBatch.Apply();
            if (miscBillboardBatch) miscBillboardBatch.Apply();

            // Enumerate start marker game objects
            EnumerateStartMarkers();
        }

        private bool ReadyCheck()
        {
            // Ensure we have a DaggerfallUnity reference
            if (dfUnity == null)
            {
                dfUnity = DaggerfallUnity.Instance;
            }

            // Do nothing if DaggerfallUnity not ready
            if (!dfUnity.IsReady)
            {
                DaggerfallUnity.LogMessage("DaggerfallLocation: DaggerfallUnity component is not ready. Have you set your Arena2 path?");
                return false;
            }

            return true;
        }

        #endregion
    }
}
