﻿using ICities;
using System;
using System.IO;

namespace MoreCityStatistics
{
    /// <summary>
    /// save/load settings and snapshots to/from the game save file
    /// </summary>
    public class MCSSerializableData : SerializableDataExtensionBase
    {
        // serialization constants
        private const string SettingsID = "MoreCityStatisticsSettings";
        private const string SnapshotsIDPrefix = "MoreCityStatisticsSnapshots";
        private const int CurrentVersion = 9;

        /// <summary>
        /// called when a game or editor is saved (including AutoSave)
        /// </summary>
        public override void OnSaveData()
        {
            // save only for game (i.e. ignore for editors)
            if (serializableDataManager.managers.loading.currentMode != AppMode.Game)
            {
                return;
            }

            // check if should save
            if (Options.instance.SaveSettingsAndSnapshots)
            {
                // lock thread while working with snapshots
                Snapshots.instance.LockThread();

                try
                {
                    // save settings to the game file
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (BinaryWriter writer = new BinaryWriter(ms))
                        {
                            // save version to the game file
                            LogUtil.LogInfo($"Saving data version [{CurrentVersion}].");
                            writer.Write(CurrentVersion);

                            // save selections for snapshot frequency, show range, and category/statistic to the game file
                            SnapshotFrequency.instance.Serialize(writer);
                            ShowRange.instance.Serialize(writer);
                            Categories.instance.Serialize(writer);

                            // logging to get settings size
                            // LogUtil.LogInfo($"Settings size = [{ms.Position}] bytes.");
                        }
                        serializableDataManager.SaveData(SettingsID, ms.ToArray());
                    }

                    // save snapshots to the game file
                    Snapshots.instance.Serialize(serializableDataManager);
                    LogUtil.LogInfo(SnapshotsLogText("Saved"));
                }
                catch (Exception ex)
                {
                    LogUtil.LogException(ex);
                }
                finally
                {
                    // make sure thread is unlocked
                    Snapshots.instance.UnlockThread();
                }
            }
            else
            {
                // erase all existing saved data from the game file
                serializableDataManager.EraseData(SettingsID);
                Snapshots.instance.EraseData(serializableDataManager);
            }
        }

        /// <summary>
        /// called when a game or editor is loaded
        /// </summary>
        public override void OnLoadData()
        {
            // snapshots are not loaded
            Snapshots.instance.Loaded = false;

            // load only for game (i.e. ignore for editors)
            if (serializableDataManager.managers.loading.currentMode != AppMode.Game)
            {
                // debug logging
                if (ConfigurationUtil<Configuration>.Load().DebugLogging)
                {
                    LogUtil.LogInfo($"MCSSerializableData.OnLoadData currentMode={serializableDataManager.managers.loading.currentMode}");
                }

                return;
            }

            // lock thread while working with snapshots
            Snapshots.instance.LockThread();

            try
            {
                // initialize before loading
                SnapshotFrequency.instance.Initialize();
                ShowRange.instance.Initialize();
                Categories.instance.Initialize();
                Snapshots.instance.Initialize();

                // load settings from the game file
                byte[] data = serializableDataManager.LoadData(SettingsID);
                if (data == null)
                {
                    // this is not an error, it just means this mod did not previously save anything
                    Snapshots.instance.Loaded = true;
                    LogUtil.LogInfo("No data to load.");
                    return;
                }

                // read settings from the game file
                int version = 0;
                using (MemoryStream ms = new MemoryStream(data))
                {
                    using (BinaryReader reader = new BinaryReader(ms))
                    {
                        // read version from the game file
                        version = reader.ReadInt32();
                        LogUtil.LogInfo($"Loading data version [{version}].");

                        // read selections for snapshot frequency, show range, and category/statistic from the game file
                        SnapshotFrequency.instance.Deserialize(reader, version);
                        ShowRange.instance.Deserialize(reader, version);
                        Categories.instance.Deserialize(reader, version);
                    }
                }

                // read snapshots from the game file
                Snapshots.instance.Deserialize(serializableDataManager, version);
                LogUtil.LogInfo(SnapshotsLogText("Loaded"));

                // success, even if no snapshots were loaded
                Snapshots.instance.Loaded = true;
            }
            catch (Exception ex)
            {
                LogUtil.LogException(ex);
            }
            finally
            {
                // make sure thread is unlocked
                Snapshots.instance.UnlockThread();
            }
        }

        /// <summary>
        /// construct a standard snapshot serialization ID to ensure it is the same everywhere
        /// </summary>
        public static string SnapshotSerializationID(int snapshotBlock)
        {
            return SnapshotsIDPrefix + snapshotBlock.ToString();
        }

        /// <summary>
        /// return log text for loaded or saved snapshots
        /// </summary>
        private static string SnapshotsLogText(string prefix)
        {
            // start with basic text using prefix and snapshot count
            int snapshotCount = Snapshots.instance.Count;
            string logText = $"{prefix} [{snapshotCount}] snapshot";

            // check for no snapshots
            if (snapshotCount == 0)
            {
                logText += "s.";
            }
            else
            {
                // include date/time range of snapshots
                const string DateTimeFormat = "yyyy/MM/dd HH:mm:ss";
                DateTime firstSnapshotDateTime = Snapshots.instance[0].SnapshotDateTime;
                DateTime lastSnapshotDateTime = Snapshots.instance[snapshotCount - 1].SnapshotDateTime;
                if (snapshotCount == 1)
                {
                    logText += $" for [{firstSnapshotDateTime.ToString(DateTimeFormat)}].";
                }
                else
                {
                    logText += $"s from [{firstSnapshotDateTime.ToString(DateTimeFormat)}] to [{lastSnapshotDateTime.ToString(DateTimeFormat)}].";
                }
            }

            // return text
            return logText;
        }
    }
}
