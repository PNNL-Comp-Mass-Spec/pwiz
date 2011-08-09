﻿//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SQLite;
using System.ComponentModel;
using System.Threading;
using System.IO;
using System.Security.AccessControl;

namespace IDPicker.DataModel
{
    public class Merger
    {
        #region Events
        public event EventHandler<MergingProgressEventArgs> MergingProgress;
        #endregion

        #region Event arguments
        public class MergingProgressEventArgs : CancelEventArgs
        {
            public int MergedFiles { get; set; }
            public int TotalFiles { get; set; }

            public Exception MergingException { get; set; }
        }
        #endregion

        /// <summary>
        /// Merge one or more idpDBs into a target idpDB.
        /// </summary>
        public Merger (string mergeTargetFilepath, IEnumerable<string> mergeSourceFilepaths)
        {
            this.mergeTargetFilepath = mergeTargetFilepath;
            this.mergeSourceFilepaths = mergeSourceFilepaths;
        }

        /// <summary>
        /// Merge an idpDB connection (either file or in-memory) to a target idpDB file.
        /// </summary>
        public Merger (string mergeTargetFilepath, SQLiteConnection mergeSourceConnection)
        {
            this.mergeTargetFilepath = mergeTargetFilepath;
            this.mergeSourceConnection = mergeSourceConnection;
        }

        public void Start ()
        {
            var workerThread = new Thread(merge);
            workerThread.Start();

            while (workerThread.IsAlive)
            {
                workerThread.Join(100);
                System.Windows.Forms.Application.DoEvents();
            }
        }

        void initializeTarget (SQLiteConnection conn)
        {
            if (!SessionFactoryFactory.IsValidFile(mergeTargetFilepath))
            {
                System.IO.File.Delete(mergeTargetFilepath);
                SessionFactoryFactory.CreateFile(mergeTargetFilepath);
            }
            else
            {
                using (var session = SessionFactoryFactory.CreateSessionFactory(mergeTargetFilepath, false, false).OpenSession())
                {
                    DataFilter.DropFilters(session.Connection);
                }
            }

            conn.ExecuteNonQuery("ATTACH DATABASE '" + mergeTargetFilepath.Replace("'", "''") + "' AS merged");
            conn.ExecuteNonQuery("PRAGMA journal_mode=OFF; PRAGMA synchronous=OFF; PRAGMA cache_size=" + tempCacheSize);
            conn.ExecuteNonQuery("PRAGMA merged.journal_mode=OFF; PRAGMA merged.synchronous=OFF; PRAGMA merged.cache_size=" + mergedCacheSize);

            conn.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS merged.MergedFiles (Filepath TEXT PRIMARY KEY)");

            string getMaxIdsSql =
                      @"SELECT (SELECT IFNULL(MAX(Id),0) FROM merged.Protein),
                               (SELECT IFNULL(MAX(Id),0) FROM merged.PeptideInstance),
                               (SELECT IFNULL(MAX(Id),0) FROM merged.Peptide),
                               (SELECT IFNULL(MAX(Id),0) FROM merged.PeptideSpectrumMatch),
                               (SELECT IFNULL(MAX(Id),0) FROM merged.PeptideSpectrumMatchScoreName),
                               (SELECT IFNULL(MAX(Id),0) FROM merged.PeptideModification),
                               (SELECT IFNULL(MAX(Id),0) FROM merged.Modification),
                               (SELECT IFNULL(MAX(Id),0) FROM merged.SpectrumSourceGroup),
                               (SELECT IFNULL(MAX(Id),0) FROM merged.SpectrumSource),
                               (SELECT IFNULL(MAX(Id),0) FROM merged.SpectrumSourceGroupLink),
                               (SELECT IFNULL(MAX(Id),0) FROM merged.Spectrum),
                               (SELECT IFNULL(MAX(Id),0) FROM merged.Analysis)
                       ";
            var maxIds = conn.ExecuteQuery(getMaxIdsSql).First();
            MaxProteinId = (long) maxIds[0];
            MaxPeptideInstanceId = (long) maxIds[1];
            MaxPeptideId = (long) maxIds[2];
            MaxPeptideSpectrumMatchId = (long) maxIds[3];
            MaxPeptideSpectrumMatchScoreNameId = (long) maxIds[4];
            MaxPeptideModificationId = (long) maxIds[5];
            MaxModificationId = (long) maxIds[6];
            MaxSpectrumSourceGroupId = (long) maxIds[7];
            MaxSpectrumSourceId = (long) maxIds[8];
            MaxSpectrumSourceGroupLinkId = (long) maxIds[9];
            MaxSpectrumId = (long) maxIds[10];
            MaxAnalysisId = (long) maxIds[11];
        }

        void merge ()
        {
            if (mergeSourceConnection != null)
                mergeConnection(mergeSourceConnection);
            else
                mergeFiles();
        }

        void mergeFiles ()
        {
            using (var conn = new SQLiteConnection("Data Source=:memory:"))
            {
                conn.Open();

                initializeTarget(conn);

                IDbTransaction transaction;

                int mergedFiles = 0;
                foreach (string mergeSourceFilepath in mergeSourceFilepaths)
                {
                    if (OnMergingProgress(null, ++mergedFiles))
                        break;

                    if (!System.IO.File.Exists(mergeSourceFilepath) || mergeSourceFilepath == mergeTargetFilepath)
                        continue;

                    // skip files that have already been merged
                    if (conn.ExecuteQuery("SELECT * FROM merged.MergedFiles WHERE Filepath = '" + mergeSourceFilepath.Replace("'", "''") + "'").Count() > 0)
                        continue;

                    // if the database can fit in the available RAM, populate the disk cache
                    long ramBytesAvailable = (long) new System.Diagnostics.PerformanceCounter("Memory", "Available Bytes").NextValue();
                    if (ramBytesAvailable > new FileInfo(mergeSourceFilepath).Length)
                    {
                        using (var fs = new FileStream(mergeSourceFilepath, FileMode.Open, FileSystemRights.ReadData, FileShare.ReadWrite, (1 << 15), FileOptions.SequentialScan))
                        {
                            var buffer = new byte[UInt16.MaxValue];
                            while (fs.Read(buffer, 0, UInt16.MaxValue) > 0) { }
                        }
                    }

                    using (var newConn = new SQLiteConnection("Data Source=\"" + mergeSourceFilepath + "\""))
                    {
                        newConn.Open();
                        DataFilter.DropFilters(newConn);
                    }

                    conn.ExecuteNonQuery("ATTACH DATABASE '" + mergeSourceFilepath.Replace("'", "''") + "' AS new");
                    conn.ExecuteNonQuery("PRAGMA new.cache_size=" + newCacheSize);

                    transaction = conn.BeginTransaction();

                    try
                    {
                        mergeProteins(conn);
                        mergePeptideInstances(conn);
                        mergeSpectrumSourceGroups(conn);
                        mergeSpectrumSources(conn);
                        mergeSpectrumSourceGroupLinks(conn);
                        mergeSpectra(conn);
                        mergeModifications(conn);
                        mergePeptideSpectrumMatchScoreNames(conn);
                        mergeAnalyses(conn);
                        addNewProteins(conn);
                        addNewPeptideInstances(conn);
                        addNewPeptides(conn);
                        addNewPeptideSequences(conn);
                        addNewModifications(conn);
                        addNewSpectrumSourceGroups(conn);
                        addNewSpectrumSources(conn);
                        addNewSpectrumSourceGroupLinks(conn);
                        addNewSpectra(conn);
                        addNewPeptideSpectrumMatches(conn);
                        addNewPeptideSpectrumMatchScoreNames(conn);
                        addNewPeptideSpectrumMatchScores(conn);
                        addNewPeptideModifications(conn);
                        addNewAnalyses(conn);
                        addNewAnalysisParameters(conn);
                        addNewQonverterSettings(conn);

                        conn.ExecuteNonQuery("INSERT INTO merged.MergedFiles VALUES ('" + mergeSourceFilepath.Replace("'", "''") + "')");
                    }
                        /*catch(Exception ex)
                {
                    if(OnMergingProgress(ex, mergedFiles))
                        break;
                }*/
                    finally
                    {
                        transaction.Commit();
                        getNewMaxIds(conn);
                        conn.ExecuteNonQuery("DETACH DATABASE new");
                    }
                }

                transaction = conn.BeginTransaction();

                try
                {
                    addIntegerSet(conn);
                }
                finally
                {
                    transaction.Commit();
                }
            }
        }

        void mergeConnection (SQLiteConnection conn)
        {
            mergeSourceDatabase = "main";

            initializeTarget(conn);

            using (IDbTransaction transaction = conn.BeginTransaction())
            {
                // FIXME: when merging idpDBs with unpopulated peptide instances,
                //        center it on Peptide/PeptideSequences, not Protein/PeptideInstance
                mergeProteins(conn);
                mergePeptideInstances(conn);
                mergeSpectrumSourceGroups(conn);
                mergeSpectrumSources(conn);
                mergeSpectrumSourceGroupLinks(conn);
                mergeSpectra(conn);
                mergeModifications(conn);
                mergePeptideSpectrumMatchScoreNames(conn);
                mergeAnalyses(conn);
                addNewProteins(conn);
                addNewPeptideInstances(conn);
                addNewPeptides(conn);
                addNewPeptideSequences(conn);
                addNewModifications(conn);
                addNewSpectrumSourceGroups(conn);
                addNewSpectrumSources(conn);
                addNewSpectrumSourceGroupLinks(conn);
                addNewSpectra(conn);
                addNewPeptideSpectrumMatches(conn);
                addNewPeptideSpectrumMatchScoreNames(conn);
                addNewPeptideSpectrumMatchScores(conn);
                addNewPeptideModifications(conn);
                addNewAnalyses(conn);
                addNewAnalysisParameters(conn);
                addNewQonverterSettings(conn);
                addIntegerSet(conn);

                transaction.Commit();
            }
            /*catch(Exception ex)
            {
                if(OnMergingProgress(ex, mergedFiles))
                    break;
            }*/
        }

        static string mergeProteinsSql =
              @"DROP TABLE IF EXISTS ProteinMergeMap;
                CREATE TABLE ProteinMergeMap (BeforeMergeId INTEGER PRIMARY KEY, AfterMergeId INT);
                INSERT INTO ProteinMergeMap
                SELECT newPro.Id, IFNULL(oldPro.Id, newPro.Id+{1})
                FROM {0}.Protein newPro
                LEFT JOIN merged.Protein oldPro ON newPro.Accession = oldPro.Accession;
                CREATE UNIQUE INDEX ProteinMergeMap_Index2 ON ProteinMergeMap (AfterMergeId);

                DROP TABLE IF EXISTS NewProteins;
                CREATE TABLE NewProteins AS
                SELECT BeforeMergeId, AfterMergeId
                FROM ProteinMergeMap
                WHERE AfterMergeId > {1}
               ";
        void mergeProteins (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(mergeProteinsSql, mergeSourceDatabase, MaxProteinId)); }


        static string mergePeptideInstancesSql =
              @"DROP TABLE IF EXISTS PeptideInstanceMergeMap;
                CREATE TABLE PeptideInstanceMergeMap (BeforeMergeId INTEGER PRIMARY KEY, AfterMergeId INTEGER, AfterMergeProtein INTEGER, BeforeMergePeptide INTEGER, AfterMergePeptide INTEGER);
                INSERT INTO PeptideInstanceMergeMap
                SELECT newInstance.Id, IFNULL(oldInstance.Id, newInstance.Id+{1}), IFNULL(oldInstance.Protein, proMerge.AfterMergeId), newInstance.Peptide, IFNULL(oldInstance.Peptide, newInstance.Peptide+{2})
                FROM ProteinMergeMap proMerge
                JOIN {0}.PeptideInstance newInstance ON proMerge.BeforeMergeId = newInstance.Protein
                LEFT JOIN merged.PeptideInstance oldInstance ON proMerge.AfterMergeId = oldInstance.Protein
                                                            AND newInstance.Length = oldInstance.Length
                                                            AND newInstance.Offset = oldInstance.Offset;
                CREATE UNIQUE INDEX PeptideInstanceMergeMap_Index2 ON PeptideInstanceMergeMap (AfterMergeId);
                CREATE INDEX PeptideInstanceMergeMap_Index3 ON PeptideInstanceMergeMap (BeforeMergePeptide);
                CREATE INDEX PeptideInstanceMergeMap_Index4 ON PeptideInstanceMergeMap (AfterMergePeptide);
               ";
        void mergePeptideInstances (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(mergePeptideInstancesSql, mergeSourceDatabase, MaxPeptideInstanceId, MaxPeptideId)); }


        static string mergeAnalysesSql =
              @"DROP TABLE IF EXISTS AnalysisMergeMap;
                CREATE TABLE AnalysisMergeMap (BeforeMergeId INTEGER PRIMARY KEY, AfterMergeId INT);
                INSERT INTO AnalysisMergeMap
                SELECT newAnalysis.Id, IFNULL(oldAnalysis.Id, newAnalysis.Id+{1})
                FROM (SELECT a.Id, SoftwareName || ' ' || SoftwareVersion || ' ' || GROUP_CONCAT(ap.Name || ' ' || ap.Value) AS DistinctKey
                      FROM {0}.Analysis a
                      LEFT JOIN {0}.AnalysisParameter ap ON a.Id = Analysis
                      GROUP BY a.Id) newAnalysis
                LEFT JOIN (SELECT a.Id, SoftwareName || ' ' || SoftwareVersion || ' ' || GROUP_CONCAT(ap.Name || ' ' || ap.Value) AS DistinctKey
                           FROM merged.Analysis a
                           LEFT JOIN merged.AnalysisParameter ap ON a.Id = Analysis
                           GROUP BY a.Id) oldAnalysis ON newAnalysis.DistinctKey = oldAnalysis.DistinctKey;
               ";
        void mergeAnalyses (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(mergeAnalysesSql, mergeSourceDatabase, MaxAnalysisId)); }


        static string mergeSpectrumSourceGroupsSql =
              @"DROP TABLE IF EXISTS SpectrumSourceGroupMergeMap;
                CREATE TABLE SpectrumSourceGroupMergeMap (BeforeMergeId INTEGER PRIMARY KEY, AfterMergeId INT);
                INSERT INTO SpectrumSourceGroupMergeMap
                SELECT newGroup.Id, IFNULL(oldGroup.Id, newGroup.Id+{1})
                FROM {0}.SpectrumSourceGroup newGroup
                LEFT JOIN merged.SpectrumSourceGroup oldGroup ON newGroup.Name = oldGroup.Name
               ";
        void mergeSpectrumSourceGroups (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(mergeSpectrumSourceGroupsSql, mergeSourceDatabase, MaxSpectrumSourceGroupId)); }


        static string mergeSpectrumSourcesSql =
              @"DROP TABLE IF EXISTS SpectrumSourceMergeMap;
                CREATE TABLE SpectrumSourceMergeMap (BeforeMergeId INTEGER PRIMARY KEY, AfterMergeId INT);
                INSERT INTO SpectrumSourceMergeMap
                SELECT newSource.Id, IFNULL(oldSource.Id, newSource.Id+{1})
                FROM {0}.SpectrumSource newSource
                LEFT JOIN merged.SpectrumSource oldSource ON newSource.Name = oldSource.Name;
                CREATE UNIQUE INDEX SpectrumSourceMergeMap_Index2 ON SpectrumSourceMergeMap (AfterMergeId);
               ";
        void mergeSpectrumSources (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(mergeSpectrumSourcesSql, mergeSourceDatabase, MaxSpectrumSourceId)); }


        static string mergeSpectrumSourceGroupLinksSql =
              @"DROP TABLE IF EXISTS SpectrumSourceGroupLinkMergeMap;
                CREATE TABLE SpectrumSourceGroupLinkMergeMap (BeforeMergeId INTEGER PRIMARY KEY, AfterMergeId INT);
                INSERT INTO SpectrumSourceGroupLinkMergeMap
                SELECT newLink.Id, IFNULL(oldLink.Id, newLink.Id+{1})
                FROM {0}.SpectrumSourceGroupLink newLink
                JOIN SpectrumSourceMergeMap ssMerge ON newLink.Source = ssMerge.BeforeMergeId
                JOIN SpectrumSourceGroupMergeMap ssgMerge ON newLink.Group_ = ssgMerge.BeforeMergeId
                LEFT JOIN merged.SpectrumSourceGroupLink oldLink ON ssMerge.AfterMergeId = oldLink.Source
                                                                AND ssgMerge.AfterMergeId = oldLink.Group_
               ";
        void mergeSpectrumSourceGroupLinks (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(mergeSpectrumSourceGroupLinksSql, mergeSourceDatabase, MaxSpectrumSourceGroupLinkId)); }


        static string mergeSpectraSql =
              @"DROP TABLE IF EXISTS SpectrumMergeMap;
                CREATE TABLE SpectrumMergeMap (BeforeMergeId INTEGER PRIMARY KEY, AfterMergeId INT);
                INSERT INTO SpectrumMergeMap
                SELECT newSpectrum.Id, IFNULL(oldSpectrum.Id, newSpectrum.Id+{1})
                FROM {0}.Spectrum newSpectrum
                JOIN SpectrumSourceMergeMap ssMerge ON newSpectrum.Source = ssMerge.BeforeMergeId
                LEFT JOIN merged.Spectrum oldSpectrum ON ssMerge.AfterMergeId = oldSpectrum.Source
                                                     AND newSpectrum.NativeID = oldSpectrum.NativeID;
               ";
        void mergeSpectra (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(mergeSpectraSql, mergeSourceDatabase, MaxSpectrumId)); }


        static string mergeModificationsSql =
              @"DROP TABLE IF EXISTS ModificationMergeMap;
                CREATE TABLE ModificationMergeMap (BeforeMergeId INTEGER PRIMARY KEY, AfterMergeId INT);
                INSERT INTO ModificationMergeMap
                SELECT newMod.Id, IFNULL(oldMod.Id, newMod.Id+{1})
                FROM {0}.Modification newMod
                LEFT JOIN merged.Modification oldMod ON IFNULL(newMod.Formula, 1) = IFNULL(oldMod.Formula, 1)
                                                    AND newMod.MonoMassDelta = oldMod.MonoMassDelta
               ";
        void mergeModifications (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(mergeModificationsSql, mergeSourceDatabase, MaxModificationId)); }


        static string mergePeptideSpectrumMatchScoreNamesSql =
              @"DROP TABLE IF EXISTS PeptideSpectrumMatchScoreNameMergeMap;
                CREATE TABLE PeptideSpectrumMatchScoreNameMergeMap (BeforeMergeId INTEGER PRIMARY KEY, AfterMergeId INT);
                INSERT INTO PeptideSpectrumMatchScoreNameMergeMap
                SELECT newName.Id, IFNULL(oldName.Id, newName.Id+{1})
                FROM {0}.PeptideSpectrumMatchScoreName newName
                LEFT JOIN merged.PeptideSpectrumMatchScoreName oldName ON newName.Name = oldName.Name
               ";
        void mergePeptideSpectrumMatchScoreNames (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(mergePeptideSpectrumMatchScoreNamesSql, mergeSourceDatabase, MaxPeptideSpectrumMatchScoreNameId)); }


        static string addNewProteinsSql =
              @"INSERT INTO merged.Protein (Id, Accession, IsDecoy, Length)
                SELECT AfterMergeId, Accession, IsDecoy, Length
                FROM NewProteins
                JOIN {0}.Protein newPro ON BeforeMergeId = newPro.Id;

                INSERT INTO merged.ProteinMetadata
                SELECT AfterMergeId, Description
                FROM NewProteins
                JOIN {0}.ProteinMetadata newPro ON BeforeMergeId = newPro.Id;

                INSERT INTO merged.ProteinData
                SELECT AfterMergeId, Sequence
                FROM NewProteins
                JOIN {0}.ProteinData newPro ON BeforeMergeId = newPro.Id
               ";
        void addNewProteins (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(addNewProteinsSql, mergeSourceDatabase)); }


        static string addNewPeptideInstancesSql =
              @"INSERT INTO merged.PeptideInstance
                SELECT AfterMergeId, AfterMergeProtein, AfterMergePeptide,
                       Offset, Length, NTerminusIsSpecific, CTerminusIsSpecific, MissedCleavages
                FROM PeptideInstanceMergeMap piMerge
                JOIN {0}.PeptideInstance newInstance ON BeforeMergeId = newInstance.Id
                WHERE AfterMergeId > {1}
               ";
        void addNewPeptideInstances (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(addNewPeptideInstancesSql, mergeSourceDatabase, MaxPeptideInstanceId)); }


        static string addNewPeptidesSql =
              @"INSERT INTO merged.Peptide
                SELECT AfterMergePeptide, MonoisotopicMass, MolecularWeight, DecoySequence
                FROM {0}.Peptide newPep
                JOIN PeptideInstanceMergeMap ON newPep.Id = BeforeMergePeptide
                WHERE AfterMergePeptide > {1}
                GROUP BY AfterMergePeptide
               ";
        void addNewPeptides (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(addNewPeptidesSql, mergeSourceDatabase, MaxPeptideId)); }


        static string addNewPeptideSequencesSql =
              @"CREATE TABLE IF NOT EXISTS merged.PeptideSequences (Id INTEGER PRIMARY KEY, Sequence TEXT UNIQUE);
                INSERT OR IGNORE INTO merged.PeptideSequences
                SELECT AfterMergePeptide, Sequence
                FROM {0}.PeptideSequences newSequence
                JOIN PeptideInstanceMergeMap ON newSequence.Id = BeforeMergePeptide
                WHERE AfterMergePeptide > {1}
                GROUP BY AfterMergePeptide
               ";
        void addNewPeptideSequences (IDbConnection conn) { try {conn.ExecuteNonQuery(String.Format(addNewPeptideSequencesSql, mergeSourceDatabase, MaxPeptideId));} catch {} }


        static string addNewSpectrumSourceGroupsSql =
              @"INSERT INTO merged.SpectrumSourceGroup
                SELECT AfterMergeId, Name
                FROM {0}.SpectrumSourceGroup newGroup
                JOIN SpectrumSourceGroupMergeMap ssgMerge ON Id = BeforeMergeId
                WHERE AfterMergeId > {1}
               ";
        void addNewSpectrumSourceGroups (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(addNewSpectrumSourceGroupsSql, mergeSourceDatabase, MaxSpectrumSourceGroupId)); }


        static string addNewSpectrumSourcesSql =
              @"INSERT INTO merged.SpectrumSource
                SELECT ssMerge.AfterMergeId, Name, URL, ssgMerge.AfterMergeId, MsDataBytes
                FROM {0}.SpectrumSource newSource
                JOIN SpectrumSourceMergeMap ssMerge ON newSource.Id = ssMerge.BeforeMergeId
                JOIN SpectrumSourceGroupMergeMap ssgMerge ON newSource.Group_ = ssgMerge.BeforeMergeId
                WHERE ssMerge.AfterMergeId > {1}
               ";
        void addNewSpectrumSources (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(addNewSpectrumSourcesSql, mergeSourceDatabase, MaxSpectrumSourceId)); }


        static string addNewSpectrumSourceGroupLinksSql =
              @"INSERT INTO merged.SpectrumSourceGroupLink
                SELECT ssglMerge.AfterMergeId, ssMerge.AfterMergeId, ssgMerge.AfterMergeId
                FROM {0}.SpectrumSourceGroupLink newLink
                JOIN SpectrumSourceMergeMap ssMerge ON newLink.Source = ssMerge.BeforeMergeId
                JOIN SpectrumSourceGroupMergeMap ssgMerge ON newLink.Group_ = ssgMerge.BeforeMergeId
                JOIN SpectrumSourceGroupLinkMergeMap ssglMerge ON newLink.Id = ssglMerge.BeforeMergeId
                WHERE ssglMerge.AfterMergeId > {1}
               ";
        void addNewSpectrumSourceGroupLinks (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(addNewSpectrumSourceGroupLinksSql, mergeSourceDatabase, MaxSpectrumSourceGroupLinkId)); }


        static string addNewSpectraSql =
              @"INSERT INTO merged.Spectrum
                SELECT sMerge.AfterMergeId, ssMerge.AfterMergeId, Index_, NativeID, PrecursorMZ
                FROM {0}.Spectrum newSpectrum
                JOIN SpectrumSourceMergeMap ssMerge ON newSpectrum.Source = ssMerge.BeforeMergeId
                JOIN SpectrumMergeMap sMerge ON newSpectrum.Id = sMerge.BeforeMergeId
                WHERE sMerge.AfterMergeId > {1}
               ";
        void addNewSpectra (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(addNewSpectraSql, mergeSourceDatabase, MaxSpectrumId)); }


        static string addNewModificationsSql =
              @"INSERT INTO merged.Modification
                SELECT AfterMergeId, MonoMassDelta, AvgMassDelta, Formula, Name
                FROM {0}.Modification newMod
                JOIN ModificationMergeMap modMerge ON newMod.Id = BeforeMergeId
                WHERE AfterMergeId > {1}
               ";
        void addNewModifications (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(addNewModificationsSql, mergeSourceDatabase, MaxModificationId)); }


        static string addNewPeptideSpectrumMatchesSql =
              @"INSERT INTO merged.PeptideSpectrumMatch
                SELECT newPSM.Id+{1}, sMerge.AfterMergeId, aMerge.AfterMergeId, AfterMergePeptide,
                       QValue, MonoisotopicMass, MolecularWeight, MonoisotopicMassError, MolecularWeightError,
                       Rank, Charge
                FROM {0}.PeptideSpectrumMatch newPSM
                JOIN PeptideInstanceMergeMap piMerge ON Peptide = BeforeMergePeptide
                JOIN AnalysisMergeMap aMerge ON Analysis = aMerge.BeforeMergeId
                JOIN SpectrumMergeMap sMerge ON Spectrum = sMerge.BeforeMergeId
                GROUP BY newPSM.Id
                ORDER BY newPSM.Id
               ";
        void addNewPeptideSpectrumMatches (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(addNewPeptideSpectrumMatchesSql, mergeSourceDatabase, MaxPeptideSpectrumMatchId)); }


        static string addNewPeptideSpectrumMatchScoreNamesSql =
              @"INSERT INTO merged.PeptideSpectrumMatchScoreName
                SELECT AfterMergeId, newName.Name
                FROM {0}.PeptideSpectrumMatchScoreName newName
                JOIN PeptideSpectrumMatchScoreNameMergeMap nameMerge ON newName.Id = BeforeMergeId
                WHERE AfterMergeId > {1}
               ";
        void addNewPeptideSpectrumMatchScoreNames (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(addNewPeptideSpectrumMatchScoreNamesSql, mergeSourceDatabase, MaxPeptideSpectrumMatchScoreNameId)); }


        static string addNewPeptideSpectrumMatchScoresSql =
              @"INSERT INTO merged.PeptideSpectrumMatchScore
                SELECT PsmId+{1}, Value, AfterMergeId
                FROM {0}.PeptideSpectrumMatchScore newScore
                JOIN PeptideSpectrumMatchScoreNameMergeMap nameMerge ON newScore.ScoreNameId = BeforeMergeId
               ";
        void addNewPeptideSpectrumMatchScores (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(addNewPeptideSpectrumMatchScoresSql, mergeSourceDatabase, MaxPeptideSpectrumMatchId)); }


        static string addNewPeptideModificationsSql =
              @"INSERT INTO merged.PeptideModification
                SELECT Id+{1}, PeptideSpectrumMatch+{2}, AfterMergeId, Offset, Site
                FROM {0}.PeptideModification newPM
                JOIN ModificationMergeMap modMerge ON Modification = BeforeMergeId
               ";
        void addNewPeptideModifications (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(addNewPeptideModificationsSql, mergeSourceDatabase, MaxPeptideModificationId, MaxPeptideSpectrumMatchId)); }


        static string addNewAnalysesSql =
              @"INSERT INTO merged.Analysis
                SELECT AfterMergeId, Name, SoftwareName, SoftwareVersion, Type, StartTime
                FROM {0}.Analysis newAnalysis
                JOIN AnalysisMergeMap aMerge ON Id = BeforeMergeId
                WHERE AfterMergeId > {1}
               ";
        void addNewAnalyses (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(addNewAnalysesSql, mergeSourceDatabase, MaxAnalysisId)); }


        static string addNewAnalysisParametersSql =
              @"INSERT INTO merged.AnalysisParameter (Analysis, Name, Value)
                SELECT AfterMergeId, Name, Value
                FROM {0}.AnalysisParameter newAP
                JOIN AnalysisMergeMap aMerge ON Analysis = BeforeMergeId
                WHERE AfterMergeId > {1}
               ";
        void addNewAnalysisParameters (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(addNewAnalysisParametersSql, mergeSourceDatabase, MaxAnalysisId)); }


        static string addNewQonverterSettingsSql =
              @"INSERT INTO merged.QonverterSettings
                SELECT AfterMergeId, QonverterMethod, DecoyPrefix, RerankMatches, Kernel, MassErrorHandling, MissedCleavagesHandling, TerminalSpecificityHandling, ChargeStateHandling, ScoreInfoByName
                FROM {0}.QonverterSettings newQS
                JOIN AnalysisMergeMap aMerge ON Id = BeforeMergeId
                WHERE AfterMergeId > {1}
               ";
        void addNewQonverterSettings (IDbConnection conn) { conn.ExecuteNonQuery(String.Format(addNewQonverterSettingsSql, mergeSourceDatabase, MaxAnalysisId)); }


        static string getNewMaxIdsSql =
              @"SELECT (SELECT IFNULL(MAX(Id),0) FROM {0}.Protein),
                       (SELECT IFNULL(MAX(Id),0) FROM {0}.PeptideInstance),
                       (SELECT IFNULL(MAX(Id),0) FROM {0}.Peptide),
                       (SELECT IFNULL(MAX(Id),0) FROM {0}.PeptideSpectrumMatch),
                       (SELECT IFNULL(MAX(Id),0) FROM {0}.PeptideSpectrumMatchScoreName),
                       (SELECT IFNULL(MAX(Id),0) FROM {0}.PeptideModification),
                       (SELECT IFNULL(MAX(Id),0) FROM {0}.Modification),
                       (SELECT IFNULL(MAX(Id),0) FROM {0}.SpectrumSourceGroup),
                       (SELECT IFNULL(MAX(Id),0) FROM {0}.SpectrumSource),
                       (SELECT IFNULL(MAX(Id),0) FROM {0}.SpectrumSourceGroupLink),
                       (SELECT IFNULL(MAX(Id),0) FROM {0}.Spectrum),
                       (SELECT IFNULL(MAX(Id),0) FROM {0}.Analysis)
               ";
        void getNewMaxIds (IDbConnection conn)
        {
            var maxIds = conn.ExecuteQuery(String.Format(getNewMaxIdsSql, mergeSourceDatabase)).First();
            MaxProteinId += (long) maxIds[0];
            MaxPeptideInstanceId += (long) maxIds[1];
            MaxPeptideId += (long) maxIds[2];
            MaxPeptideSpectrumMatchId += (long) maxIds[3];
            MaxPeptideSpectrumMatchScoreNameId += (long) maxIds[4];
            MaxPeptideModificationId += (long) maxIds[5];
            MaxModificationId += (long) maxIds[6];
            MaxSpectrumSourceGroupId += (long) maxIds[7];
            MaxSpectrumSourceId += (long) maxIds[8];
            MaxSpectrumSourceGroupLinkId += (long) maxIds[9];
            MaxSpectrumId += (long) maxIds[10];
            MaxAnalysisId += (long) maxIds[11];
        }

        static void addIntegerSet (IDbConnection conn)
        {
            long maxInteger = (long) conn.ExecuteQuery("SELECT IFNULL(MAX(Value),0) FROM IntegerSet").First()[0];
            long maxProteinLength = (long) conn.ExecuteQuery("SELECT IFNULL(MAX(LENGTH(Sequence)),0) FROM ProteinData").First()[0];

            var cmd = conn.CreateCommand(); cmd.CommandText = "INSERT INTO IntegerSet VALUES (?)";
            var iParam = cmd.CreateParameter(); cmd.Parameters.Add(iParam);

            for (long i = maxInteger + 1; i <= maxProteinLength; ++i)
            {
                iParam.Value = i;
                cmd.ExecuteNonQuery();
            }
        }

        bool OnMergingProgress (Exception ex, int mergedFiles)
        {
            if (MergingProgress != null)
            {
                var eventArgs = new MergingProgressEventArgs()
                {
                    MergedFiles = mergedFiles,
                    TotalFiles = mergeSourceFilepaths.Count(),
                    MergingException = ex
                };
                MergingProgress(this, eventArgs);
                return eventArgs.Cancel;
            }
            else if (ex != null)
                throw ex;

            return false;
        }

        string mergeTargetFilepath;
        IEnumerable<string> mergeSourceFilepaths;
        SQLiteConnection mergeSourceConnection;
        string mergeSourceDatabase = "new";

        long MaxProteinId = 0;
        long MaxPeptideInstanceId = 0;
        long MaxPeptideId = 0;
        long MaxPeptideSpectrumMatchId = 0;
        long MaxPeptideSpectrumMatchScoreNameId = 0;
        long MaxPeptideModificationId = 0;
        long MaxModificationId = 0;
        long MaxSpectrumSourceGroupId = 0;
        long MaxSpectrumSourceId = 0;
        long MaxSpectrumSourceGroupLinkId = 0;
        long MaxSpectrumId = 0;
        long MaxAnalysisId = 0;

        static int tempCacheSize = 1000000;
        static int mergedCacheSize = 800000;
        static int newCacheSize = 50000;
    }
}