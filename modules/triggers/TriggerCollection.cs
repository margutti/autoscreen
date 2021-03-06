﻿//-----------------------------------------------------------------------
// <copyright file="TriggerCollection.cs" company="Gavin Kendall">
//     Copyright (c) 2020 Gavin Kendall
// </copyright>
// <author>Gavin Kendall</author>
// <summary>A collection of triggers.</summary>
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.
//-----------------------------------------------------------------------
using System;
using System.Text;
using System.Xml;

namespace AutoScreenCapture
{
    /// <summary>
    /// A collection class to store and manage Trigger objects.
    /// </summary>
    public class TriggerCollection : CollectionTemplate<Trigger>
    {
        private const string XML_FILE_INDENT_CHARS = "   ";
        private const string XML_FILE_TRIGGER_NODE = "trigger";
        private const string XML_FILE_TRIGGERS_NODE = "triggers";
        private const string XML_FILE_ROOT_NODE = "autoscreen";

        private const string TRIGGER_NAME = "name";
        private const string TRIGGER_CONDITION = "condition";
        private const string TRIGGER_ACTION = "action";
        private const string TRIGGER_EDITOR = "editor";
        private const string TRIGGER_ACTIVE = "active";
        private const string TRIGGER_DATE = "date";
        private const string TRIGGER_TIME = "time";
        private const string TRIGGER_SCREEN_CAPTURE_INTERVAL = "screen_capture_interval";
        private const string TRIGGER_MODULE_ITEM = "module_item";

        private readonly string TRIGGER_XPATH;

        private static string AppCodename { get; set; }
        private static string AppVersion { get; set; }

        /// <summary>
        /// Empty constructor.
        /// </summary>
        public TriggerCollection()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("/");
            sb.Append(XML_FILE_ROOT_NODE);
            sb.Append("/");
            sb.Append(XML_FILE_TRIGGERS_NODE);
            sb.Append("/");
            sb.Append(XML_FILE_TRIGGER_NODE);

            TRIGGER_XPATH = sb.ToString();
        }

        /// <summary>
        /// Loads the triggers.
        /// </summary>
        public bool LoadXmlFileAndAddTriggers()
        {
            try
            {
                if (FileSystem.FileExists(FileSystem.TriggersFile))
                {
                    XmlDocument xDoc = new XmlDocument();
                    xDoc.Load(FileSystem.TriggersFile);

                    AppVersion = xDoc.SelectSingleNode("/autoscreen").Attributes["app:version"]?.Value;
                    AppCodename = xDoc.SelectSingleNode("/autoscreen").Attributes["app:codename"]?.Value;

                    XmlNodeList xTriggers = xDoc.SelectNodes(TRIGGER_XPATH);

                    foreach (XmlNode xTrigger in xTriggers)
                    {
                        Trigger trigger = new Trigger();
                        XmlNodeReader xReader = new XmlNodeReader(xTrigger);

                        while (xReader.Read())
                        {
                            if (xReader.IsStartElement() && !xReader.IsEmptyElement)
                            {
                                switch (xReader.Name)
                                {
                                    case TRIGGER_NAME:
                                        xReader.Read();
                                        trigger.Name = xReader.Value;
                                        break;

                                    case TRIGGER_CONDITION:
                                        xReader.Read();
                                        trigger.ConditionType =
                                            (TriggerConditionType) Enum.Parse(typeof(TriggerConditionType), xReader.Value);
                                        break;

                                    case TRIGGER_ACTION:
                                        xReader.Read();
                                        trigger.ActionType =
                                            (TriggerActionType) Enum.Parse(typeof(TriggerActionType), xReader.Value);
                                        break;

                                    // This is purely for backwards compatibility with older versions.
                                    // We no longer use Editor. We use ModuleItem now (since 2.3.0.0).
                                    case TRIGGER_EDITOR:
                                        xReader.Read();
                                        trigger.ModuleItem = xReader.Value;
                                        break;

                                    case TRIGGER_ACTIVE:
                                        xReader.Read();
                                        trigger.Active = Convert.ToBoolean(xReader.Value);
                                        break;

                                    case TRIGGER_DATE:
                                        xReader.Read();
                                        trigger.Date = Convert.ToDateTime(xReader.Value);
                                        break;

                                    case TRIGGER_TIME:
                                        xReader.Read();
                                        trigger.Time = Convert.ToDateTime(xReader.Value);
                                        break;

                                    case TRIGGER_SCREEN_CAPTURE_INTERVAL:
                                        xReader.Read();
                                        trigger.ScreenCaptureInterval = Convert.ToInt32(xReader.Value);
                                        break;

                                    case TRIGGER_MODULE_ITEM:
                                        xReader.Read();
                                        trigger.ModuleItem = xReader.Value;
                                        break;
                                }
                            }
                        }

                        xReader.Close();

                        // Change the data for each Trigger that's being loaded if we've detected that
                        // the XML document is from an older version of the application.
                        if (Settings.VersionManager.IsOldAppVersion(AppCodename, AppVersion))
                        {
                            Log.WriteDebugMessage("An old version of the triggers.xml file was detected. Attempting upgrade to new schema.");

                            Version v2300 = Settings.VersionManager.Versions.Get("Boombayah", "2.3.0.0");
                            Version configVersion = Settings.VersionManager.Versions.Get(AppCodename, AppVersion);

                            if (v2300 != null && configVersion != null && configVersion.VersionNumber < v2300.VersionNumber)
                            {
                                Log.WriteDebugMessage("Dalek 2.2.4.6 or older detected");

                                // These are new properties for Trigger that were introduced in 2.3.0.0
                                // so any version before 2.3.0.0 needs to have them during an upgrade.
                                trigger.Active = true;
                                trigger.Date = DateTime.Now;
                                trigger.Time = DateTime.Now;
                                trigger.ScreenCaptureInterval = 0;
                            }
                        }

                        if (!string.IsNullOrEmpty(trigger.Name))
                        {
                            Add(trigger);
                        }
                    }

                    if (Settings.VersionManager.IsOldAppVersion(AppCodename, AppVersion))
                    {
                        SaveToXmlFile();
                    }
                }
                else
                {
                    Log.WriteDebugMessage("WARNING: Unable to load triggers");

                    Trigger triggerApplicationStartShowInterface = new Trigger()
                    {
                        Active = true,
                        Name = "Application Startup -> Show",
                        ConditionType = TriggerConditionType.ApplicationStartup,
                        ActionType = TriggerActionType.ShowInterface,
                        Date = DateTime.Now,
                        Time = DateTime.Now,
                        ScreenCaptureInterval = 0
                    };

                    Trigger triggerScreenCaptureStartedHideInterface = new Trigger()
                    {
                        Active = true,
                        Name = "Capture Started -> Hide",
                        ConditionType = TriggerConditionType.ScreenCaptureStarted,
                        ActionType = TriggerActionType.HideInterface,
                        Date = DateTime.Now,
                        Time = DateTime.Now,
                        ScreenCaptureInterval = 0
                    };

                    Trigger triggerScreenCaptureStoppedShowInterface = new Trigger()
                    {
                        Active = true,
                        Name = "Capture Stopped -> Show",
                        ConditionType = TriggerConditionType.ScreenCaptureStopped,
                        ActionType = TriggerActionType.ShowInterface,
                        Date = DateTime.Now,
                        Time = DateTime.Now,
                        ScreenCaptureInterval = 0
                    };

                    Trigger triggerInterfaceClosingExitApplication = new Trigger()
                    {
                        Active = true,
                        Name = "Interface Closing -> Exit",
                        ConditionType = TriggerConditionType.InterfaceClosing,
                        ActionType = TriggerActionType.ExitApplication,
                        Date = DateTime.Now,
                        Time = DateTime.Now,
                        ScreenCaptureInterval = 0
                    };

                    Trigger triggerLimitReachedStopScreenCapture = new Trigger()
                    {
                        Active = true,
                        Name = "Limit Reached -> Stop",
                        ConditionType = TriggerConditionType.LimitReached,
                        ActionType = TriggerActionType.StopScreenCapture,
                        Date = DateTime.Now,
                        Time = DateTime.Now,
                        ScreenCaptureInterval = 0
                    };

                    // Setup a few "built in" triggers by default.
                    Add(triggerApplicationStartShowInterface);
                    Add(triggerScreenCaptureStartedHideInterface);
                    Add(triggerScreenCaptureStoppedShowInterface);
                    Add(triggerInterfaceClosingExitApplication);
                    Add(triggerLimitReachedStopScreenCapture);

                    SaveToXmlFile();
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.WriteExceptionMessage("TriggerCollection::LoadXmlFileAndAddTriggers", ex);

                return false;
            }
        }

        /// <summary>
        /// Saves the triggers.
        /// </summary>
        public bool SaveToXmlFile()
        {
            try
            {
                XmlWriterSettings xSettings = new XmlWriterSettings();
                xSettings.Indent = true;
                xSettings.CloseOutput = true;
                xSettings.CheckCharacters = true;
                xSettings.Encoding = Encoding.UTF8;
                xSettings.NewLineChars = Environment.NewLine;
                xSettings.IndentChars = XML_FILE_INDENT_CHARS;
                xSettings.NewLineHandling = NewLineHandling.Entitize;
                xSettings.ConformanceLevel = ConformanceLevel.Document;

                if (string.IsNullOrEmpty(FileSystem.TriggersFile))
                {
                    FileSystem.TriggersFile = FileSystem.DefaultTriggersFile;

                    if (FileSystem.FileExists(FileSystem.ConfigFile))
                    {
                        FileSystem.AppendToFile(FileSystem.ConfigFile, "\nTriggersFile=" + FileSystem.TriggersFile);
                    }
                }

                if (FileSystem.FileExists(FileSystem.TriggersFile))
                {
                    FileSystem.DeleteFile(FileSystem.TriggersFile);
                }

                using (XmlWriter xWriter =
                    XmlWriter.Create(FileSystem.TriggersFile, xSettings))
                {
                    xWriter.WriteStartDocument();
                    xWriter.WriteStartElement(XML_FILE_ROOT_NODE);
                    xWriter.WriteAttributeString("app", "version", XML_FILE_ROOT_NODE, Settings.ApplicationVersion);
                    xWriter.WriteAttributeString("app", "codename", XML_FILE_ROOT_NODE, Settings.ApplicationCodename);
                    xWriter.WriteStartElement(XML_FILE_TRIGGERS_NODE);

                    foreach (Trigger trigger in base.Collection)
                    {
                        xWriter.WriteStartElement(XML_FILE_TRIGGER_NODE);

                        xWriter.WriteElementString(TRIGGER_ACTIVE, trigger.Active.ToString());
                        xWriter.WriteElementString(TRIGGER_NAME, trigger.Name);
                        xWriter.WriteElementString(TRIGGER_CONDITION, trigger.ConditionType.ToString());
                        xWriter.WriteElementString(TRIGGER_ACTION, trigger.ActionType.ToString());
                        xWriter.WriteElementString(TRIGGER_DATE, trigger.Date.ToString());
                        xWriter.WriteElementString(TRIGGER_TIME, trigger.Time.ToString());
                        xWriter.WriteElementString(TRIGGER_SCREEN_CAPTURE_INTERVAL, trigger.ScreenCaptureInterval.ToString());
                        xWriter.WriteElementString(TRIGGER_MODULE_ITEM, trigger.ModuleItem);

                        xWriter.WriteEndElement();
                    }

                    xWriter.WriteEndElement();
                    xWriter.WriteEndElement();
                    xWriter.WriteEndDocument();

                    xWriter.Flush();
                    xWriter.Close();
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.WriteExceptionMessage("TriggerCollection::SaveToXmlFile", ex);

                return false;
            }
        }
    }
}