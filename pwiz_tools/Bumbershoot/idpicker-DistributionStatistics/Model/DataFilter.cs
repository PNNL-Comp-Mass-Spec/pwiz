﻿//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
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
using IDPicker.DataModel;
using NHibernate.Linq;

namespace IDPicker.DataModel
{
    public class DataFilter : EventArgs
    {
        #region Events
        public event EventHandler<FilteringProgressEventArgs> FilteringProgress;
        #endregion

        #region Event arguments
        public class FilteringProgressEventArgs : System.ComponentModel.CancelEventArgs
        {
            public FilteringProgressEventArgs(string stage, int completed, Exception ex)
            {
                CompletedFilters = completed;
                TotalFilters = 16;
                FilteringStage = stage;
                FilteringException = ex;
            }

            public int CompletedFilters { get; protected set; }
            public int TotalFilters { get; protected set; }

            public string FilteringStage { get; protected set; }
            public Exception FilteringException { get; protected set; }
        }
        #endregion

        protected bool OnFilteringProgress (FilteringProgressEventArgs e)
        {
            if (FilteringProgress != null)
                FilteringProgress(this, e);
            return e.Cancel;
        }

        public DataFilter ()
        {
            MaximumQValue = 0.02;
            MinimumDistinctPeptidesPerProtein = 2;
            MinimumSpectraPerProtein = 2;
            MinimumAdditionalPeptidesPerProtein = 1;
            MinimumSpectraPerDistinctMatch = 1;
            MinimumSpectraPerDistinctPeptide = 1;
            MaximumProteinGroupsPerPeptide = 5;

            DistinctMatchFormat = new DistinctMatchFormat()
            {
                AreModificationsDistinct = true,
                IsAnalysisDistinct = false,
                IsChargeDistinct = true,
                ModificationMassRoundToNearest = 1m
            };
        }

        public DataFilter (DataFilter other)
        {
            MaximumQValue = other.MaximumQValue;
            MinimumDistinctPeptidesPerProtein = other.MinimumDistinctPeptidesPerProtein;
            MinimumSpectraPerProtein = other.MinimumSpectraPerProtein;
            MinimumAdditionalPeptidesPerProtein = other.MinimumAdditionalPeptidesPerProtein;
            MinimumSpectraPerDistinctMatch = other.MinimumSpectraPerDistinctMatch;
            MinimumSpectraPerDistinctPeptide = other.MinimumSpectraPerDistinctPeptide;
            DistinctMatchFormat = other.DistinctMatchFormat;
            Cluster = other.Cluster == null ? null : new List<int>(other.Cluster);
            ProteinGroup = other.ProteinGroup == null ? null : new List<int>(other.ProteinGroup);
            Protein = other.Protein == null ? null : new List<Protein>(other.Protein);
            PeptideGroup = other.PeptideGroup == null ? null : new List<int>(other.PeptideGroup);
            Peptide = other.Peptide == null ? null : new List<Peptide>(other.Peptide);
            DistinctMatchKey = other.DistinctMatchKey == null ? null : new List<DistinctMatchKey>(other.DistinctMatchKey);
            Modifications = other.Modifications == null ? null : new List<Modification>(other.Modifications);
            ModifiedSite = other.ModifiedSite == null ? null : new List<char>(other.ModifiedSite);
            Charge = other.Charge == null ? null : new List<int>(other.Charge);
            Analysis = other.Analysis == null ? null : new List<Analysis>(other.Analysis);
            Spectrum = other.Spectrum == null ? null : new List<Spectrum>(other.Spectrum);
            SpectrumSource = other.SpectrumSource == null ? null : new List<SpectrumSource>(other.SpectrumSource);
            SpectrumSourceGroup = other.SpectrumSourceGroup == null ? null : new List<SpectrumSourceGroup>(other.SpectrumSourceGroup);
            AminoAcidOffset = other.AminoAcidOffset == null ? null : new List<int>(other.AminoAcidOffset);
        }

        public double MaximumQValue { get; set; }
        public int MinimumDistinctPeptidesPerProtein { get; set; }
        public int MinimumSpectraPerProtein { get; set; }
        public int MinimumAdditionalPeptidesPerProtein { get; set; }
        public int MinimumSpectraPerDistinctMatch { get; set; }
        public int MinimumSpectraPerDistinctPeptide { get; set; }
        public int MaximumProteinGroupsPerPeptide { get; set; }

        public DistinctMatchFormat DistinctMatchFormat { get; set; }

        public IList<int> Cluster { get; set; }
        public IList<int> ProteinGroup { get; set; }
        public IList<int> PeptideGroup { get; set; }
        public IList<Protein> Protein { get; set; }
        public IList<Peptide> Peptide { get; set; }
        public IList<DistinctMatchKey> DistinctMatchKey { get; set; }
        public IList<Modification> Modifications { get; set; }
        public IList<char> ModifiedSite { get; set; }
        public IList<int> Charge { get; set; }
        public IList<SpectrumSourceGroup> SpectrumSourceGroup { get; set; }
        public IList<SpectrumSource> SpectrumSource { get; set; }
        public IList<Spectrum> Spectrum { get; set; }
        public IList<Analysis> Analysis { get; set; }

        /// <summary>
        /// A list of amino acid offsets to filter on; any peptide which contains
        /// one of the specified offsets passes the filter.
        /// </summary>
        public IList<int> AminoAcidOffset { get; set; }

        /// <summary>
        /// A regular expression for filtering peptide sequences based on amino-acid composition.
        /// </summary>
        /// <example>To match peptides with at least one histidine: "*H*"</example>
        /// <example>To match peptides with at least two histidines: "*H*H*</example>
        /// <example>To match peptides with two adjacent histidines: "*HH*</example>
        /// <example>To match peptides starting with glutamine: "Q*"</example>
        /// <example>To match peptides with a  histidines: "*H*H*</example>
        public string Composition { get; set; }

        public object FilterSource { get; set; }

        public bool IsBasicFilter
        {
            get
            {
                return Cluster.IsNullOrEmpty() && ProteinGroup.IsNullOrEmpty() && Protein.IsNullOrEmpty() &&
                       PeptideGroup.IsNullOrEmpty() && Peptide.IsNullOrEmpty() && DistinctMatchKey.IsNullOrEmpty() &&
                       Modifications.IsNullOrEmpty() && ModifiedSite.IsNullOrEmpty() &&
                       SpectrumSourceGroup.IsNullOrEmpty() && SpectrumSource.IsNullOrEmpty() &&
                       Spectrum.IsNullOrEmpty() && Charge.IsNullOrEmpty() && Analysis.IsNullOrEmpty() &&
                       AminoAcidOffset.IsNullOrEmpty() && Composition.IsNullOrEmpty();
            }
        }

        private static bool NullSafeSequenceEqual<T> (IEnumerable<T> lhs, IEnumerable<T> rhs)
        {
            if (lhs == rhs)
                return true;
            else if (lhs == null || rhs == null)
                return false;
            else
                return lhs.SequenceEqual<T>(rhs);
        }

        private static IList<T> NullSafeSequenceUnion<T> (IEnumerable<T> lhs, IEnumerable<T> rhs)
        {
            if (lhs == null && rhs == null)
                return null;
            else if (lhs == null)
                return rhs.ToList();
            else if (rhs == null)
                return lhs.ToList();
            else if (lhs == rhs)
                return lhs.ToList();
            else
                return lhs.Union<T>(rhs).ToList();
        }

        public override int GetHashCode () { return (this as object).GetHashCode(); }

        public override bool Equals (object obj)
        {
            var other = obj as DataFilter;
            if (other == null)
                return false;
            else if (object.ReferenceEquals(this, obj))
                return true;

            return MaximumQValue == other.MaximumQValue &&
                   MinimumDistinctPeptidesPerProtein == other.MinimumDistinctPeptidesPerProtein &&
                   MinimumSpectraPerProtein == other.MinimumSpectraPerProtein &&
                   MinimumAdditionalPeptidesPerProtein == other.MinimumAdditionalPeptidesPerProtein &&
                   MinimumSpectraPerDistinctMatch == other.MinimumSpectraPerDistinctMatch &&
                   MinimumSpectraPerDistinctPeptide == other.MinimumSpectraPerDistinctPeptide &&
                   MaximumProteinGroupsPerPeptide == other.MaximumProteinGroupsPerPeptide &&
                   NullSafeSequenceEqual(Cluster, other.Cluster) &&
                   NullSafeSequenceEqual(ProteinGroup, other.ProteinGroup) &&
                   NullSafeSequenceEqual(PeptideGroup, other.PeptideGroup) &&
                   NullSafeSequenceEqual(Protein, other.Protein) &&
                   NullSafeSequenceEqual(Peptide, other.Peptide) &&
                   NullSafeSequenceEqual(DistinctMatchKey, other.DistinctMatchKey) &&
                   NullSafeSequenceEqual(Modifications, other.Modifications) &&
                   NullSafeSequenceEqual(ModifiedSite, other.ModifiedSite) &&
                   NullSafeSequenceEqual(Charge, other.Charge) &&
                   NullSafeSequenceEqual(Analysis, other.Analysis) &&
                   NullSafeSequenceEqual(Spectrum, other.Spectrum) &&
                   NullSafeSequenceEqual(SpectrumSource, other.SpectrumSource) &&
                   NullSafeSequenceEqual(SpectrumSourceGroup, other.SpectrumSourceGroup) &&
                   NullSafeSequenceEqual(AminoAcidOffset, other.AminoAcidOffset) &&
                   Composition == other.Composition &&
                   DistinctMatchFormat == other.DistinctMatchFormat;
        }

        public static DataFilter operator + (DataFilter lhs, DataFilter rhs)
        {
            var newFilter = new DataFilter(lhs);
            newFilter.Cluster = NullSafeSequenceUnion(newFilter.Cluster, rhs.Cluster);
            newFilter.ProteinGroup = NullSafeSequenceUnion(newFilter.ProteinGroup, rhs.ProteinGroup);
            newFilter.PeptideGroup = NullSafeSequenceUnion(newFilter.PeptideGroup, rhs.PeptideGroup);
            newFilter.Protein = NullSafeSequenceUnion(newFilter.Protein, rhs.Protein);
            newFilter.Peptide = NullSafeSequenceUnion(newFilter.Peptide, rhs.Peptide);
            newFilter.DistinctMatchKey = NullSafeSequenceUnion(newFilter.DistinctMatchKey, rhs.DistinctMatchKey);
            newFilter.Modifications = NullSafeSequenceUnion(newFilter.Modifications, rhs.Modifications);
            newFilter.ModifiedSite = NullSafeSequenceUnion(newFilter.ModifiedSite, rhs.ModifiedSite);
            newFilter.Charge = NullSafeSequenceUnion(newFilter.Charge, rhs.Charge);
            newFilter.Analysis = NullSafeSequenceUnion(newFilter.Analysis, rhs.Analysis);
            newFilter.Spectrum = NullSafeSequenceUnion(newFilter.Spectrum, rhs.Spectrum);
            newFilter.SpectrumSource = NullSafeSequenceUnion(newFilter.SpectrumSource, rhs.SpectrumSource);
            newFilter.SpectrumSourceGroup = NullSafeSequenceUnion(newFilter.SpectrumSourceGroup, rhs.SpectrumSourceGroup);
            newFilter.AminoAcidOffset = NullSafeSequenceUnion(newFilter.AminoAcidOffset, rhs.AminoAcidOffset);
            newFilter.Composition = rhs.Composition;
            return newFilter;
        }

        public static bool operator == (DataFilter lhs, DataFilter rhs) { return object.ReferenceEquals(lhs, null) ? object.ReferenceEquals(rhs, null) : lhs.Equals(rhs); }
        public static bool operator != (DataFilter lhs, DataFilter rhs) { return !(lhs == rhs); }

        private void toStringHelper<T> (string memberSingular, string memberPlural, IEnumerable<T> memberList,
                                        Func<T, string> memberLambda, IList<string> toStringResult)
        {
            if (memberList.IsNullOrEmpty())
                return;

            var distinctMemberList = memberList.Select(memberLambda).Distinct();
            if (distinctMemberList.Count() > 1)
                toStringResult.Add(String.Format("{0} {1}", distinctMemberList.Count(), memberPlural.ToLower()));
            else
                toStringResult.Add(String.Format("{0} {1}", memberSingular, distinctMemberList.First()));
        }

        private void toStringHelper<T> (string member, IEnumerable<T> memberList, Func<T, string> memberLambda,
                                        IList<string> toStringResult)
        {
            toStringHelper(member, member + "s", memberList, memberLambda, toStringResult);
        }

        private void toStringHelper<T> (string member, IEnumerable<T> memberList, IList<string> toStringResult)
        {
            toStringHelper(member, memberList, o => o.ToString(), toStringResult);
        }

        public override string ToString ()
        {
            var result = new List<string>();
            toStringHelper("Cluster", Cluster, result);
            toStringHelper("Protein group", ProteinGroup, result);
            toStringHelper("Peptide group", PeptideGroup, result);
            toStringHelper("Protein", Protein, o => o.Accession, result);
            toStringHelper("Peptide", Peptide, o => o.Sequence, result);
            toStringHelper("Distinct match", "Distinct matches", DistinctMatchKey, o => o.ToString(), result);
            toStringHelper("Modified site", ModifiedSite, result);
            toStringHelper("Mass shift", Modifications, o => Math.Round(o.MonoMassDelta).ToString(), result);
            toStringHelper("Charge", Charge, result);
            toStringHelper("Analysis", "Analyses", Analysis, o => o.Name, result);
            toStringHelper("Group", SpectrumSourceGroup, o => o.Name, result);
            toStringHelper("Source", SpectrumSource, o => o.Name, result);
            toStringHelper("Spectrum", "Spectra", Spectrum, o => o.NativeID, result);
            toStringHelper("Offset", AminoAcidOffset, o => (o + 1).ToString(), result);

            if (!Composition.IsNullOrEmpty())
                result.Add(String.Format("Composition \"{0}\"", Composition));

            if (result.Count > 0)
                return String.Join("; ", result.ToArray());

            return String.Format("Q-value ≤ {0}; " +
                                 "Distinct peptides per protein ≥ {1}; " +
                                 "Spectra per protein ≥ {2}; " +
                                 "Additional peptides per protein ≥ {3}" +
                                 "Spectra per distinct match ≥ {4}; " +
                                 "Spectra per distinct peptide ≥ {5}; ",
                                 "Protein groups per peptide ≤ {6}",
                                 MaximumQValue,
                                 MinimumDistinctPeptidesPerProtein,
                                 MinimumSpectraPerProtein,
                                 MinimumAdditionalPeptidesPerProtein,
                                 MinimumSpectraPerDistinctMatch,
                                 MinimumSpectraPerDistinctPeptide,
                                 MaximumProteinGroupsPerPeptide);
        }

        public static DataFilter LoadFilter (NHibernate.ISession session)
        {
            try
            {
                var filteringCriteria = session.CreateSQLQuery(@"SELECT MaximumQValue,
                                                                        MinimumDistinctPeptidesPerProtein,
                                                                        MinimumSpectraPerProtein,
                                                                        MinimumAdditionalPeptidesPerProtein,
                                                                        MinimumSpectraPerDistinctMatch,
                                                                        MinimumSpectraPerDistinctPeptide,
                                                                        MaximumProteinGroupsPerPeptide
                                                                 FROM FilteringCriteria
                                                                ").List<object[]>()[0];
                var dataFilter = new DataFilter();
                dataFilter.MaximumQValue = Convert.ToDouble(filteringCriteria[0]);
                dataFilter.MinimumDistinctPeptidesPerProtein = Convert.ToInt32(filteringCriteria[1]);
                dataFilter.MinimumSpectraPerProtein = Convert.ToInt32(filteringCriteria[2]);
                dataFilter.MinimumAdditionalPeptidesPerProtein = Convert.ToInt32(filteringCriteria[3]);
                dataFilter.MinimumSpectraPerDistinctMatch = Convert.ToInt32(filteringCriteria[4]);
                dataFilter.MinimumSpectraPerDistinctPeptide = Convert.ToInt32(filteringCriteria[5]);
                dataFilter.MaximumProteinGroupsPerPeptide = Convert.ToInt32(filteringCriteria[6]);
                return dataFilter;
            }
            catch
            {
                return null;
            }
        }

        static string saveFilterSql = @"DROP TABLE IF EXISTS FilteringCriteria;
                                        CREATE TABLE IF NOT EXISTS FilteringCriteria
                                        (
                                         MaximumQValue NUMERIC,
                                         MinimumDistinctPeptidesPerProtein INT,
                                         MinimumSpectraPerProtein INT,
                                         MinimumAdditionalPeptidesPerProtein INT,
                                         MinimumSpectraPerDistinctMatch INT,
                                         MinimumSpectraPerDistinctPeptide INT,
                                         MaximumProteinGroupsPerPeptide INT
                                        );
                                        INSERT INTO FilteringCriteria SELECT {0}, {1}, {2}, {3}, {4}, {5}, {6}
                                       ";
        private void SaveFilter(NHibernate.ISession session)
        {
            session.CreateSQLQuery(String.Format(saveFilterSql,
                                                 MaximumQValue,
                                                 MinimumDistinctPeptidesPerProtein,
                                                 MinimumSpectraPerProtein,
                                                 MinimumAdditionalPeptidesPerProtein,
                                                 MinimumSpectraPerDistinctMatch,
                                                 MinimumSpectraPerDistinctPeptide,
                                                 MaximumProteinGroupsPerPeptide)).ExecuteUpdate();
        }

        public string GetBasicQueryStringSQL ()
        {
            return String.Format("FROM PeptideSpectrumMatch psm " +
                                 "JOIN PeptideInstance pi ON psm.Peptide = pi.Peptide " +
                                 "WHERE psm.QValue <= {0} " +
                                 "GROUP BY pi.Protein " +
                                 "HAVING {1} <= COUNT(DISTINCT (psm.Peptide || ' ' || psm.MonoisotopicMass || ' ' || psm.Charge)) AND " +
                                 "       {2} <= COUNT(DISTINCT psm.Spectrum)",
                                 MaximumQValue,
                                 MinimumDistinctPeptidesPerProtein,
                                 MinimumSpectraPerProtein);
        }

        public static void DropFilters (System.Data.IDbConnection conn)
        {
            // ignore errors if main tables haven't been created yet

            #region Drop Filtered* tables
            conn.ExecuteNonQuery(@"DROP TABLE IF EXISTS FilteredProtein;
                                   DROP TABLE IF EXISTS FilteredPeptideInstance;
                                   DROP TABLE IF EXISTS FilteredPeptide;
                                   DROP TABLE IF EXISTS FilteredPeptideSpectrumMatch
                                  ");
            #endregion

            #region Restore Unfiltered* tables as the main tables
            try
            {
                // if unfiltered tables have not been created, this will throw and skip the rest of the block
                conn.ExecuteNonQuery("SELECT Id FROM UnfilteredProtein LIMIT 1");

                // drop filtered tables
                conn.ExecuteNonQuery(@"DROP TABLE IF EXISTS Protein;
                                       DROP TABLE IF EXISTS PeptideInstance;
                                       DROP TABLE IF EXISTS Peptide;
                                       DROP TABLE IF EXISTS PeptideSpectrumMatch
                                      ");

                // rename unfiltered tables 
                conn.ExecuteNonQuery(@"ALTER TABLE UnfilteredProtein RENAME TO Protein;
                                       ALTER TABLE UnfilteredPeptideInstance RENAME TO PeptideInstance;
                                       ALTER TABLE UnfilteredPeptide RENAME TO Peptide;
                                       ALTER TABLE UnfilteredPeptideSpectrumMatch RENAME TO PeptideSpectrumMatch
                                      ");

                // reset QValues
                //conn.ExecuteNonQuery("UPDATE PeptideSpectrumMatch SET QValue = 2");
            }
            catch
            {
            }
            #endregion
        }

        public void DropFilters (NHibernate.ISession session)
        {
            DropFilters(session.Connection);
        }

        public void ApplyBasicFilters (NHibernate.ISession session)
        {
            // free up memory
            session.Clear();

            bool useScopedTransaction = !session.Transaction.IsActive;
            if (useScopedTransaction)
                session.Transaction.Begin();

            int stepsCompleted = 0;

            if (OnFilteringProgress(new FilteringProgressEventArgs("Dropping current filters...", ++stepsCompleted, null)))
                return;

            DropFilters(session);

            #region Create Filtered* tables by applying the basic filters to the main tables
            if (OnFilteringProgress(new FilteringProgressEventArgs("Filtering proteins...", ++stepsCompleted, null)))
                return;
            string filterProteinsSql =
                @"CREATE TABLE FilteredProtein (Id INTEGER PRIMARY KEY, Accession TEXT, IsDecoy INT, Cluster INT, ProteinGroup INT, Length INT);
                  INSERT INTO FilteredProtein SELECT pro.*
                  FROM PeptideSpectrumMatch psm
                  JOIN PeptideInstance pi ON psm.Peptide = pi.Peptide
                  JOIN Protein pro ON pi.Protein = pro.Id
                  JOIN Spectrum s ON psm.Spectrum = s.Id
                  JOIN SpectrumSource ss ON s.Source = ss.Id
                  -- filter out ungrouped spectrum sources
                  WHERE ss.Group_ AND {0} >= psm.QValue AND psm.Rank = 1
                  GROUP BY pi.Protein
                  HAVING {1} <= COUNT(DISTINCT psm.Peptide) AND
                         {2} <= COUNT(DISTINCT psm.Spectrum);
                  CREATE UNIQUE INDEX FilteredProtein_Accession ON FilteredProtein (Accession);";
            session.CreateSQLQuery(String.Format(filterProteinsSql,
                                                 MaximumQValue,
                                                 MinimumDistinctPeptidesPerProtein,
                                                 MinimumSpectraPerProtein)).ExecuteUpdate();

            if (OnFilteringProgress(new FilteringProgressEventArgs("Filtering peptide spectrum matches...", ++stepsCompleted, null)))
                return;
            session.CreateSQLQuery(@"CREATE TABLE FilteredPeptideSpectrumMatch (Id INTEGER PRIMARY KEY, Spectrum INT, Analysis INT, Peptide INT, QValue NUMERIC, ObservedNeutralMass NUMERIC, MonoisotopicMassError NUMERIC, MolecularWeightError NUMERIC, Rank INT, Charge INT);
                                     INSERT INTO FilteredPeptideSpectrumMatch SELECT psm.*
                                     FROM FilteredProtein pro
                                     JOIN PeptideInstance pi ON pro.Id = pi.Protein
                                     JOIN PeptideSpectrumMatch psm ON pi.Peptide = psm.Peptide
                                     JOIN Spectrum s ON psm.Spectrum = s.Id
                                     JOIN SpectrumSource ss ON s.Source = ss.Id
                                     -- filter out ungrouped spectrum sources
                                     WHERE ss.Group_ AND " + MaximumQValue + @" >= psm.QValue AND psm.Rank = 1
                                     GROUP BY psm.Id;
                                     CREATE INDEX FilteredPeptideSpectrumMatch_PeptideSpectrumAnalysis ON FilteredPeptideSpectrumMatch (Peptide, Spectrum, Analysis);
                                     CREATE INDEX FilteredPeptideSpectrumMatch_AnalysisSpectrumPeptide ON FilteredPeptideSpectrumMatch (Analysis, Spectrum, Peptide);
                                     CREATE INDEX FilteredPeptideSpectrumMatch_SpectrumPeptideAnalysis ON FilteredPeptideSpectrumMatch (Spectrum, Peptide, Analysis);
                                    "
                                  ).ExecuteUpdate();

            if (MinimumSpectraPerDistinctMatch > 1)
                session.CreateSQLQuery(@"DELETE FROM FilteredPeptideSpectrumMatch
                                         WHERE " + DistinctMatchFormat.SqlExpression.Replace("psm.", "FilteredPeptideSpectrumMatch.") + @" IN
                                               (SELECT " + DistinctMatchFormat.SqlExpression + @"
                                                FROM FilteredPeptideSpectrumMatch psm
                                                GROUP BY " + DistinctMatchFormat.SqlExpression + @"
                                                HAVING " + MinimumSpectraPerDistinctMatch + @" > COUNT(DISTINCT psm.Spectrum))
                                        ").ExecuteUpdate();

            if (OnFilteringProgress(new FilteringProgressEventArgs("Filtering peptides...", ++stepsCompleted, null)))
                return;
            session.CreateSQLQuery(@"CREATE TABLE FilteredPeptide (Id INTEGER PRIMARY KEY, MonoisotopicMass NUMERIC, MolecularWeight NUMERIC, PeptideGroup INT, DecoySequence TEXT);
                                     INSERT INTO FilteredPeptide SELECT pep.*
                                     FROM FilteredPeptideSpectrumMatch psm
                                     JOIN Peptide pep ON psm.Peptide = pep.Id
                                     GROUP BY pep.Id " +
                                     (MinimumSpectraPerDistinctPeptide > 1 ? @"HAVING " + MinimumSpectraPerDistinctPeptide + @" <= COUNT(DISTINCT psm.Spectrum)"
                                                                           : @"")
                                    //"
                                  ).ExecuteUpdate();

            if (MinimumSpectraPerDistinctMatch + MinimumSpectraPerDistinctPeptide > 1)
                session.CreateSQLQuery(@"DELETE FROM FilteredPeptideSpectrumMatch WHERE Peptide NOT IN (SELECT Id FROM FilteredPeptide);
                                        ").ExecuteUpdate();

            if (OnFilteringProgress(new FilteringProgressEventArgs("Filtering peptide instances...", ++stepsCompleted, null)))
                return;
            session.CreateSQLQuery(@"CREATE TABLE FilteredPeptideInstance (Id INTEGER PRIMARY KEY, Protein INT, Peptide INT, Offset INT, Length INT, NTerminusIsSpecific INT, CTerminusIsSpecific INT, MissedCleavages INT);
                                     INSERT INTO FilteredPeptideInstance SELECT pi.*
                                     FROM FilteredPeptide pep
                                     JOIN PeptideInstance pi ON pep.Id = pi.Peptide
                                     JOIN FilteredProtein pro ON pi.Protein = pro.Id;
                                     CREATE INDEX FilteredPeptideInstance_Protein ON FilteredPeptideInstance (Protein);
                                     CREATE INDEX FilteredPeptideInstance_Peptide ON FilteredPeptideInstance (Peptide);
                                     CREATE INDEX FilteredPeptideInstance_PeptideProtein ON FilteredPeptideInstance (Peptide, Protein);
                                     CREATE INDEX FilteredPeptideInstance_ProteinOffsetLength ON FilteredPeptideInstance (Protein, Offset, Length);"
                                  ).ExecuteUpdate();
            #endregion

            #region Rename main tables to Unfiltered*
            session.CreateSQLQuery(@"ALTER TABLE Protein RENAME TO UnfilteredProtein;
                                     ALTER TABLE PeptideInstance RENAME TO UnfilteredPeptideInstance;
                                     ALTER TABLE Peptide RENAME TO UnfilteredPeptide;
                                     ALTER TABLE PeptideSpectrumMatch RENAME TO UnfilteredPeptideSpectrumMatch
                                    ").ExecuteUpdate();
            #endregion

            #region Rename Filtered* tables to main tables
            session.CreateSQLQuery(@"ALTER TABLE FilteredProtein RENAME TO Protein;
                                     ALTER TABLE FilteredPeptideInstance RENAME TO PeptideInstance;
                                     ALTER TABLE FilteredPeptide RENAME TO Peptide;
                                     ALTER TABLE FilteredPeptideSpectrumMatch RENAME TO PeptideSpectrumMatch
                                    ").ExecuteUpdate();
            #endregion

            if (AssembleProteinGroups(session, ref stepsCompleted)) return;

            if (MaximumProteinGroupsPerPeptide > 0)
            {
                session.CreateSQLQuery(@"DELETE FROM Peptide WHERE Id IN
                                             (
                                              SELECT pi.Peptide
                                              FROM Protein pro
                                              JOIN PeptideInstance pi on pro.Id = pi.Protein
                                              GROUP BY pi.Peptide
                                              HAVING COUNT(DISTINCT ProteinGroup) > " + MaximumProteinGroupsPerPeptide + @"
                                             );
                                          DELETE FROM PeptideInstance WHERE Peptide NOT IN (SELECT Id FROM Peptide);
                                          DELETE FROM Protein WHERE Id NOT IN (SELECT Protein FROM PeptideInstance);
                                          DELETE FROM PeptideSpectrumMatch WHERE Peptide NOT IN (SELECT Id FROM Peptide);
                                         ").ExecuteUpdate();
                --stepsCompleted;
                session.CreateSQLQuery("DROP INDEX Protein_ProteinGroup").ExecuteUpdate();
                if (AssembleProteinGroups(session, ref stepsCompleted)) return;
            }

            if (ApplyAdditionalPeptidesFilter(session, ref stepsCompleted)) return;
            if (AssembleClusters(session, ref stepsCompleted)) return;
            if (AssembleProteinCoverage(session, ref stepsCompleted)) return;
            if (AssembleDistinctMatches(session, ref stepsCompleted)) return;

            // assemble new protein groups after the additional peptides filter
            session.CreateSQLQuery("DROP INDEX Protein_ProteinGroup").ExecuteUpdate();
            if (AssembleProteinGroups(session, ref stepsCompleted)) return;
            if (AssemblePeptideGroups(session, ref stepsCompleted)) return;

            SaveFilter(session);

            if (useScopedTransaction)
                session.Transaction.Commit();
        }

        #region Implementation of basic filters
        /// <summary>
        /// Set ProteinGroup column (the groups change depending on the basic filters applied)
        /// </summary>
        bool AssembleProteinGroups(NHibernate.ISession session, ref int stepsCompleted)
        {
            if (OnFilteringProgress(new FilteringProgressEventArgs("Assembling protein groups...", ++stepsCompleted, null)))
                return true;

            session.CreateSQLQuery(@"CREATE TEMP TABLE ProteinGroups AS
                                     SELECT pro.Id AS ProteinId, GROUP_CONCAT(DISTINCT pi.Peptide) AS ProteinGroup
                                     FROM PeptideInstance pi
                                     JOIN Protein pro ON pi.Protein = pro.Id
                                     GROUP BY pi.Protein;

                                     -- ProteinGroup will be a continuous sequence starting at 1
                                     CREATE TEMP TABLE TempProtein AS
                                     SELECT ProteinId, Accession, IsDecoy, Cluster, pg2.rowid, Length
                                     FROM ProteinGroups pg
                                     JOIN ( 
                                           SELECT pg.ProteinGroup
                                           FROM ProteinGroups pg
                                           GROUP BY pg.ProteinGroup
                                          ) pg2 ON pg.ProteinGroup = pg2.ProteinGroup
                                     JOIN Protein pro ON pg.ProteinId = pro.Id;

                                     DELETE FROM Protein;
                                     INSERT INTO Protein SELECT * FROM TempProtein;
                                     CREATE INDEX Protein_ProteinGroup ON Protein (ProteinGroup);
                                     DROP TABLE ProteinGroups;
                                     DROP TABLE TempProtein;
                                    ").ExecuteUpdate();

            session.Clear();

            return false;
        }

        /// <summary>
        /// Set PeptideGroup column (the groups change depending on the basic filters applied)
        /// </summary>
        bool AssemblePeptideGroups (NHibernate.ISession session, ref int stepsCompleted)
        {
            if (OnFilteringProgress(new FilteringProgressEventArgs("Assembling peptide groups...", ++stepsCompleted, null)))
                return true;

            session.CreateSQLQuery(@"CREATE TEMP TABLE PeptideGroups AS
                                     SELECT pep.Id AS PeptideId, GROUP_CONCAT(DISTINCT pi.Protein) AS PeptideGroup
                                     FROM PeptideInstance pi
                                     JOIN Peptide pep ON pi.Peptide=pep.Id
                                     GROUP BY pi.Peptide;

                                     -- PeptideGroup will be a continuous sequence starting at 1
                                     CREATE TEMP TABLE TempPeptide AS
                                     SELECT PeptideId, MonoisotopicMass, MolecularWeight, pg2.rowid, DecoySequence
                                     FROM PeptideGroups pg
                                     JOIN ( 
                                           SELECT pg.PeptideGroup
                                           FROM PeptideGroups pg
                                           GROUP BY pg.PeptideGroup
                                          ) pg2 ON pg.PeptideGroup = pg2.PeptideGroup
                                     JOIN Peptide pro ON pg.PeptideId = pro.Id;

                                     DELETE FROM Peptide;
                                     INSERT INTO Peptide SELECT * FROM TempPeptide;
                                     CREATE INDEX Peptide_PeptideGroup ON Peptide (PeptideGroup);
                                     DROP TABLE PeptideGroups;
                                     DROP TABLE TempPeptide;
                                    ").ExecuteUpdate();
            session.Clear();

            return false;
        }

        /// <summary>
        /// Calculate additional peptides per protein and filter out proteins that don't meet the minimum
        /// </summary>
        bool ApplyAdditionalPeptidesFilter (NHibernate.ISession session, ref int stepsCompleted)
        {
            if (MinimumAdditionalPeptidesPerProtein == 0)
                return OnFilteringProgress(new FilteringProgressEventArgs("Skipping additional peptide filter...", stepsCompleted += 2, null));

            if (OnFilteringProgress(new FilteringProgressEventArgs("Calculating additional peptide counts...", ++stepsCompleted, null)))
                return true;

            Map<long, long> additionalPeptidesByProteinId = CalculateAdditionalPeptides(session);

            session.CreateSQLQuery(@"DROP TABLE IF EXISTS AdditionalMatches;
                                     CREATE TABLE AdditionalMatches (ProteinId INTEGER PRIMARY KEY, AdditionalMatches INT)
                                    ").ExecuteUpdate();

            var cmd = session.Connection.CreateCommand();
            cmd.CommandText = "INSERT INTO AdditionalMatches VALUES (?, ?)";
            var parameters = new List<System.Data.IDbDataParameter>();
            for (int i = 0; i < 2; ++i)
            {
                parameters.Add(cmd.CreateParameter());
                cmd.Parameters.Add(parameters[i]);
            }
            cmd.Prepare();
            foreach (Map<long, long>.MapPair itr in additionalPeptidesByProteinId)
            {
                parameters[0].Value = itr.Key;
                parameters[1].Value = itr.Value;
                cmd.ExecuteNonQuery();
            }

            if (OnFilteringProgress(new FilteringProgressEventArgs("Filtering by additional peptide count...", ++stepsCompleted, null)))
                return true;

            // delete proteins that don't meet the additional matches filter
            // delete peptide instances whose protein is gone
            // delete peptides that no longer have any peptide instances
            // delete PSMs whose peptide is gone
            string additionalPeptidesDeleteSql = @"DELETE FROM Protein
                                                         WHERE Id IN (SELECT pro.Id
                                                                      FROM Protein pro
                                                                      JOIN AdditionalMatches am ON pro.Id = am.ProteinId
                                                                      WHERE am.AdditionalMatches < {0});
                                                   DELETE FROM PeptideInstance WHERE Protein NOT IN (SELECT Id FROM Protein);
                                                   DELETE FROM Peptide WHERE Id NOT IN (SELECT Peptide FROM PeptideInstance);
                                                   DELETE FROM PeptideSpectrumMatch WHERE Peptide NOT IN (SELECT Id FROM Peptide);
                                                  ";

            session.CreateSQLQuery(String.Format(additionalPeptidesDeleteSql, MinimumAdditionalPeptidesPerProtein)).ExecuteUpdate();

            return false;
        }

        /// <summary>
        /// Calculate clusters (connected components) for proteins
        /// </summary>
        bool AssembleClusters (NHibernate.ISession session, ref int stepsCompleted)
        {
            if (OnFilteringProgress(new FilteringProgressEventArgs("Calculating protein clusters...", ++stepsCompleted, null)))
                return true;

            Map<long, long> clusterByProteinId = calculateProteinClusters(session);

            if (OnFilteringProgress(new FilteringProgressEventArgs("Assigning proteins to clusters...", ++stepsCompleted, null)))
                return true;

            var cmd = session.Connection.CreateCommand();
            cmd.CommandText = "UPDATE Protein SET Cluster = ? WHERE Id = ?";
            var parameters = new List<System.Data.IDbDataParameter>();
            for (int i = 0; i < 2; ++i)
            {
                parameters.Add(cmd.CreateParameter());
                cmd.Parameters.Add(parameters[i]);
            }
            cmd.Prepare();
            foreach (Map<long, long>.MapPair itr in clusterByProteinId)
            {
                parameters[0].Value = itr.Value;
                parameters[1].Value = itr.Key;
                cmd.ExecuteNonQuery();
            }
            cmd.ExecuteNonQuery("CREATE INDEX Protein_Cluster ON Protein (Cluster)");

            return false;
        }

        /// <summary>
        /// Calculate coverage and coverage masks for proteins
        /// </summary>
        bool AssembleProteinCoverage (NHibernate.ISession session, ref int stepsCompleted)
        {
            if (OnFilteringProgress(new FilteringProgressEventArgs("Calculating protein coverage...", ++stepsCompleted, null)))
                return true;
            
            session.CreateSQLQuery(@"DELETE FROM ProteinCoverage;
                                     INSERT INTO ProteinCoverage (Id, Coverage)
                                     SELECT pi.Protein, CAST(COUNT(DISTINCT i.Value) AS REAL) * 100 / pro.Length
                                     FROM PeptideInstance pi
                                     JOIN Protein pro ON pi.Protein=pro.Id
                                     JOIN ProteinData pd ON pi.Protein=pd.Id
                                     JOIN IntegerSet i
                                     WHERE i.Value BETWEEN pi.Offset AND pi.Offset+pi.Length-1
                                     GROUP BY pi.Protein;
                                    ").ExecuteUpdate();

            if (OnFilteringProgress(new FilteringProgressEventArgs("Calculating protein coverage masks...", ++stepsCompleted, null)))
                return true;

            // get non-zero coverage depths at each protein offset
            var coverageMaskRows = session.CreateSQLQuery(
                                   @"SELECT pi.Protein, pro.Length, i.Value, COUNT(i.Value)
                                     FROM PeptideInstance pi
                                     JOIN Protein pro ON pi.Protein=pro.Id
                                     JOIN ProteinData pd ON pi.Protein=pd.Id
                                     JOIN IntegerSet i 
                                     WHERE i.Value BETWEEN pi.Offset AND pi.Offset+pi.Length-1
                                     GROUP BY pi.Protein, i.Value
                                     ORDER BY pi.Protein, i.Value;
                                    ").List().OfType<object[]>();

            if (OnFilteringProgress(new FilteringProgressEventArgs("Updating protein coverage masks...", ++stepsCompleted, null)))
                return true;

            var cmd = session.Connection.CreateCommand();
            cmd.CommandText = "UPDATE ProteinCoverage SET CoverageMask = ? WHERE Id = ?";
            var parameters = new List<System.Data.IDbDataParameter>();
            for (int i = 0; i < 2; ++i)
            {
                parameters.Add(cmd.CreateParameter());
                cmd.Parameters.Add(parameters[i]);
            }
            cmd.Prepare();

            var proteinCoverageMaskUserType = new ProteinCoverageMaskUserType();
            long currentProteinId = 0;
            ushort[] currentProteinMask = null;

            foreach (object[] row in coverageMaskRows)
            {
                long proteinId = Convert.ToInt64(row[0]);
                int proteinLength = Convert.ToInt32(row[1]);

                // before moving on to the next protein, update the current one
                if (proteinId > currentProteinId)
                {
                    if (currentProteinMask != null)
                    {
                        parameters[0].Value = proteinCoverageMaskUserType.Disassemble(currentProteinMask);
                        parameters[1].Value = currentProteinId;
                        cmd.ExecuteNonQuery();
                    }

                    currentProteinId = proteinId;
                    currentProteinMask = new ushort[proteinLength];

                    // initialize all offsets to 0 (no coverage)
                    currentProteinMask.Initialize();
                }

                // set a covered offset to its coverage depth
                currentProteinMask[Convert.ToInt32(row[2])] = Convert.ToUInt16(row[3]);
            }

            // set the last protein's mask
            if (currentProteinMask != null)
            {
                parameters[0].Value = proteinCoverageMaskUserType.Disassemble(currentProteinMask);
                parameters[1].Value = currentProteinId;
                cmd.ExecuteNonQuery();
            }

            return false;
        }

        bool AssembleDistinctMatches (NHibernate.ISession session, ref int stepsCompleted)
        {
            if (OnFilteringProgress(new FilteringProgressEventArgs("Assembling distinct matches...", ++stepsCompleted, null)))
                return true;
            string sql = String.Format(@"DROP TABLE IF EXISTS DistinctMatch;
                                         CREATE TABLE DistinctMatch (PsmId INTEGER PRIMARY KEY, DistinctMatchKey TEXT);
                                         INSERT INTO DistinctMatch (PsmId, DistinctMatchKey)
                                         SELECT DISTINCT psm.Id, {0}
                                         FROM PeptideSpectrumMatch psm;
                                        ", DistinctMatchFormat.SqlExpression);
            session.CreateSQLQuery(sql).ExecuteUpdate();
            return false;
        }
        #endregion

        #region Definitions for common HQL strings
        public static readonly string FromProtein = "Protein pro";
        public static readonly string FromPeptide = "Peptide pep";
        public static readonly string FromPeptideSpectrumMatch = "PeptideSpectrumMatch psm";
        public static readonly string FromPeptideInstance = "PeptideInstance pi";
        public static readonly string FromPeptideModification = "PeptideModification pm";
        public static readonly string FromModification = "Modification mod";
        public static readonly string FromAnalysis = "Analysis a";
        public static readonly string FromSpectrum = "Spectrum s";
        public static readonly string FromSpectrumSource = "SpectrumSource ss";
        public static readonly string FromSpectrumSourceGroupLink = "SpectrumSourceGroupLink ssgl";
        public static readonly string FromSpectrumSourceGroup = "SpectrumSourceGroup ssg";

        public static readonly string ProteinToPeptideInstance = "JOIN pro.Peptides pi";
        public static readonly string ProteinToPeptide = ProteinToPeptideInstance + ";JOIN pi.Peptide pep";
        public static readonly string ProteinToPeptideSpectrumMatch = ProteinToPeptide + ";JOIN pep.Matches psm";
        public static readonly string ProteinToPeptideModification = ProteinToPeptideSpectrumMatch + ";LEFT JOIN psm.Modifications pm";
        public static readonly string ProteinToModification = ProteinToPeptideModification + ";LEFT JOIN pm.Modification mod";
        public static readonly string ProteinToAnalysis = ProteinToPeptideSpectrumMatch + ";JOIN psm.Analysis a";
        public static readonly string ProteinToSpectrum = ProteinToPeptideSpectrumMatch + ";JOIN psm.Spectrum s";
        public static readonly string ProteinToSpectrumSource = ProteinToSpectrum + ";JOIN s.Source ss";
        public static readonly string ProteinToSpectrumSourceGroupLink = ProteinToSpectrumSource + ";JOIN ss.Groups ssgl";
        public static readonly string ProteinToSpectrumSourceGroup = ProteinToSpectrumSourceGroupLink + ";JOIN ssgl.Group ssg";

        public static readonly string PeptideToPeptideInstance = "JOIN pep.Instances pi";
        public static readonly string PeptideToPeptideSpectrumMatch = "JOIN pep.Matches psm";
        public static readonly string PeptideToProtein = PeptideToPeptideInstance + ";JOIN pi.Protein pro";
        public static readonly string PeptideToPeptideModification = PeptideToPeptideSpectrumMatch + ";LEFT JOIN psm.Modifications pm";
        public static readonly string PeptideToModification = PeptideToPeptideModification + ";LEFT JOIN pm.Modification mod";
        public static readonly string PeptideToAnalysis = PeptideToPeptideSpectrumMatch + ";JOIN psm.Analysis a";
        public static readonly string PeptideToSpectrum = PeptideToPeptideSpectrumMatch + ";JOIN psm.Spectrum s";
        public static readonly string PeptideToSpectrumSource = PeptideToSpectrum + ";JOIN s.Source ss";
        public static readonly string PeptideToSpectrumSourceGroupLink = PeptideToSpectrumSource + ";JOIN ss.Groups ssgl";
        public static readonly string PeptideToSpectrumSourceGroup = PeptideToSpectrumSourceGroupLink + ";JOIN ssgl.Group ssg";

        public static readonly string PeptideSpectrumMatchToPeptide = "JOIN psm.Peptide pep";
        public static readonly string PeptideSpectrumMatchToAnalysis = "JOIN psm.Analysis a";
        public static readonly string PeptideSpectrumMatchToSpectrum = "JOIN psm.Spectrum s";
        public static readonly string PeptideSpectrumMatchToPeptideModification = "LEFT JOIN psm.Modifications pm";
        public static readonly string PeptideSpectrumMatchToPeptideInstance = PeptideSpectrumMatchToPeptide + ";JOIN pep.Instances pi";
        public static readonly string PeptideSpectrumMatchToProtein = PeptideSpectrumMatchToPeptideInstance + ";JOIN pi.Protein pro";
        public static readonly string PeptideSpectrumMatchToModification = PeptideSpectrumMatchToPeptideModification + ";LEFT JOIN pm.Modification mod";
        public static readonly string PeptideSpectrumMatchToSpectrumSource = PeptideSpectrumMatchToSpectrum + ";JOIN s.Source ss";
        public static readonly string PeptideSpectrumMatchToSpectrumSourceGroupLink = PeptideSpectrumMatchToSpectrumSource + ";JOIN ss.Groups ssgl";
        public static readonly string PeptideSpectrumMatchToSpectrumSourceGroup = PeptideSpectrumMatchToSpectrumSourceGroupLink + ";JOIN ssgl.Group ssg";
        #endregion

        public string GetBasicQueryString (string fromTable, params string[] joinTables)
        {
            var joins = new Map<int, object>();
            foreach (var join in joinTables)
                foreach (var branch in join.ToString().Split(';'))
                    joins.Add(joins.Count, branch);

            var query = new StringBuilder();

            query.AppendFormat(" FROM {0} ", fromTable);
            foreach (var join in joins.Values.Distinct())
                query.AppendFormat("{0} ", join);
            query.Append(" ");

            return query.ToString();
        }

        public string GetFilteredQueryString (string fromTable, params string[] joinTables)
        {
            var joins = new Map<int, object>();
            foreach (var join in joinTables)
                foreach (var branch in join.ToString().Split(';'))
                    joins.Add(joins.Count, branch);

            // these different condition sets are AND'd together, but within each set (except for mods) they are OR'd
            var proteinConditions = new List<string>();
            var clusterConditions = new List<string>();
            var peptideConditions = new List<string>();
            var spectrumConditions = new List<string>();
            var modConditions = new List<string>();
            var otherConditions = new List<string>();

            if (fromTable == FromProtein)
            {
                if (!Peptide.IsNullOrEmpty() || !AminoAcidOffset.IsNullOrEmpty())
                    foreach (var branch in ProteinToPeptideInstance.Split(';'))
                        joins.Add(joins.Count, branch);

                if (!PeptideGroup.IsNullOrEmpty())
                    foreach (var branch in ProteinToPeptide.Split(';'))
                        joins.Add(joins.Count, branch);

                if (!Modifications.IsNullOrEmpty() || !ModifiedSite.IsNullOrEmpty())
                    foreach (var branch in ProteinToPeptideModification.Split(';'))
                        joins.Add(joins.Count, branch);

                if (!DistinctMatchKey.IsNullOrEmpty() || !Analysis.IsNullOrEmpty() ||
                    !Spectrum.IsNullOrEmpty() || !Charge.IsNullOrEmpty())
                    foreach (var branch in ProteinToPeptideSpectrumMatch.Split(';'))
                        joins.Add(joins.Count, branch);

                if (!SpectrumSource.IsNullOrEmpty())
                    foreach (var branch in ProteinToSpectrumSource.Split(';'))
                        joins.Add(joins.Count, branch);

                if (!SpectrumSourceGroup.IsNullOrEmpty())
                    foreach (var branch in ProteinToSpectrumSourceGroupLink.Split(';'))
                        joins.Add(joins.Count, branch);
            }
            else if (fromTable == FromPeptideSpectrumMatch)
            {
                if (!Cluster.IsNullOrEmpty() || !ProteinGroup.IsNullOrEmpty())
                    foreach (var branch in PeptideSpectrumMatchToProtein.Split(';'))
                        joins.Add(joins.Count, branch);

                if (!Protein.IsNullOrEmpty())
                    foreach (var branch in PeptideSpectrumMatchToPeptideInstance.Split(';'))
                        joins.Add(joins.Count, branch);

                if (!AminoAcidOffset.IsNullOrEmpty())
                {
                    // MaxValue indicates any peptide at a protein C-terminus,
                    // so the Protein table must be joined to access the Length column
                    string path = AminoAcidOffset.Contains(Int32.MaxValue) ? PeptideSpectrumMatchToProtein : PeptideSpectrumMatchToPeptideInstance;
                    foreach (var branch in path.Split(';'))
                        joins.Add(joins.Count, branch);
                }

                if (!PeptideGroup.IsNullOrEmpty())
                    foreach (var branch in PeptideSpectrumMatchToPeptide.Split(';'))
                        joins.Add(joins.Count, branch);

                if (!Modifications.IsNullOrEmpty() || !ModifiedSite.IsNullOrEmpty())
                    foreach (var branch in PeptideSpectrumMatchToPeptideModification.Split(';'))
                        joins.Add(joins.Count, branch);

                if (!SpectrumSource.IsNullOrEmpty())
                    foreach (var branch in PeptideSpectrumMatchToSpectrumSource.Split(';'))
                        joins.Add(joins.Count, branch);

                if (!SpectrumSourceGroup.IsNullOrEmpty())
                    foreach (var branch in PeptideSpectrumMatchToSpectrumSourceGroupLink.Split(';'))
                        joins.Add(joins.Count, branch);
            }

            if (!Cluster.IsNullOrEmpty())
                clusterConditions.Add(String.Format("pro.Cluster IN ({0})", String.Join(",", Cluster.Select(o => o.ToString()).ToArray())));

            if (!ProteinGroup.IsNullOrEmpty())
                proteinConditions.Add(String.Format("pro.ProteinGroup IN ({0})", String.Join(",", ProteinGroup.Select(o => o.ToString()).ToArray())));

            if (!Protein.IsNullOrEmpty())
            {
                string column = fromTable == FromProtein || joins.Any(o => ((string) o.Value).EndsWith(" pro")) ? "pro.id" : "pi.Protein.id";
                proteinConditions.Add(String.Format("{0} IN ({1})", column, String.Join(",", Protein.Select(o => o.Id.ToString()).ToArray())));
            }

            if (!PeptideGroup.IsNullOrEmpty())
                peptideConditions.Add(String.Format("pep.PeptideGroup IN ({0})", String.Join(",", PeptideGroup.Select(o => o.ToString()).ToArray())));

            if (!Peptide.IsNullOrEmpty())
            {
                string column = joins.Any(o => ((string) o.Value).EndsWith(" pi")) ? "pi.Peptide.id" : "psm.Peptide.id";
                peptideConditions.Add(String.Format("{0} IN ({1})", column, String.Join(",", Peptide.Select(o => o.Id.ToString()).ToArray())));
            }

            if (!DistinctMatchKey.IsNullOrEmpty())
                peptideConditions.Add(String.Format("psm.DistinctMatchKey IN ('{0}')", String.Join("','", DistinctMatchKey.Select(o=> o.Key).ToArray())));

            if (!ModifiedSite.IsNullOrEmpty())
                modConditions.Add(String.Format("pm.Site IN ('{0}')", String.Join("','", ModifiedSite.Select(o => o.ToString()).ToArray())));

            if (!Modifications.IsNullOrEmpty())
                modConditions.Add(String.Format("pm.Modification.id IN ({0})", String.Join(",", Modifications.Select(o => o.Id.ToString()).ToArray())));

            if (!Charge.IsNullOrEmpty())
                otherConditions.Add(String.Format("psm.Charge IN ({0})", String.Join(",", Charge.Select(o => o.ToString()).ToArray())));

            if (!Analysis.IsNullOrEmpty())
                otherConditions.Add(String.Format("psm.Analysis.id IN ({0})", String.Join(",", Analysis.Select(o => o.Id.ToString()).ToArray())));

            if (!Spectrum.IsNullOrEmpty())
                spectrumConditions.Add(String.Format("psm.Spectrum.id IN ({0})", String.Join(",", Spectrum.Select(o => o.Id.ToString()).ToArray())));

            if (!SpectrumSource.IsNullOrEmpty())
                spectrumConditions.Add(String.Format("psm.Spectrum.Source.id IN ({0})", String.Join(",", SpectrumSource.Select(o => o.Id.ToString()).ToArray())));

            if (!SpectrumSourceGroup.IsNullOrEmpty())
                spectrumConditions.Add(String.Format("ssgl.Group.id IN ({0})", String.Join(",", SpectrumSourceGroup.Select(o => o.Id.ToString()).ToArray())));

            if (!AminoAcidOffset.IsNullOrEmpty())
            {
                var offsetConditions = new List<string>();
                foreach (int offset in AminoAcidOffset)
                {
                    if (offset <= 0)
                        offsetConditions.Add("pi.Offset = 0"); // protein N-terminus
                    else if (offset == Int32.MaxValue)
                        offsetConditions.Add("pi.Offset+pi.Length = pro.Length"); // protein C-terminus
                    else
                        offsetConditions.Add(String.Format("(pi.Offset <= {0} AND pi.Offset+pi.Length > {0})", offset));
                }

                otherConditions.Add("(" + String.Join(" OR ", offsetConditions.ToArray()) + ")");
            }

            var query = new StringBuilder();

            query.AppendFormat(" FROM {0} ", fromTable);
            foreach (var join in joins.Values.Distinct())
                query.AppendFormat("{0} ", join);
            query.Append(" ");

            var conditions = new List<string>();
            if (proteinConditions.Count > 0) conditions.Add("(" + String.Join(" OR ", proteinConditions.ToArray()) + ")");
            if (clusterConditions.Count > 0) conditions.Add("(" + String.Join(" OR ", clusterConditions.ToArray()) + ")");
            if (peptideConditions.Count > 0) conditions.Add("(" + String.Join(" OR ", peptideConditions.ToArray()) + ")");
            if (spectrumConditions.Count > 0) conditions.Add("(" + String.Join(" OR ", spectrumConditions.ToArray()) + ")");
            if (modConditions.Count > 0) conditions.Add("(" + String.Join(" AND ", modConditions.ToArray()) + ")");
            if (otherConditions.Count > 0) conditions.Add("(" + String.Join(" AND ", otherConditions.ToArray()) + ")");

            if (conditions.Count > 0)
            {
                query.Append(" WHERE ");
                query.Append(String.Join(" AND ", conditions.ToArray()));
                query.Append(" ");
            }

            return query.ToString();
        }

        public string GetFilteredSqlWhereClause ()
        {
            // these different condition sets are AND'd together, but within each set (except for mods) they are OR'd
            var proteinConditions = new List<string>();
            var peptideConditions = new List<string>();
            var spectrumConditions = new List<string>();
            var modConditions = new List<string>();
            var otherConditions = new List<string>();

            if (!Cluster.IsNullOrEmpty())
                proteinConditions.Add(String.Format("pro.Cluster IN ({0})", String.Join(",", Cluster.Select(o => o.ToString()).ToArray())));

            if (!ProteinGroup.IsNullOrEmpty())
                proteinConditions.Add(String.Format("pro.ProteinGroup IN ({0})", String.Join(",", ProteinGroup.Select(o => o.ToString()).ToArray())));

            if (!Protein.IsNullOrEmpty())
                proteinConditions.Add(String.Format("pi.Protein IN ({0})", String.Join(",", Protein.Select(o => o.Id.ToString()).ToArray())));

            if (!PeptideGroup.IsNullOrEmpty())
                peptideConditions.Add(String.Format("pep.PeptideGroup IN ({0})", String.Join(",", PeptideGroup.Select(o => o.ToString()).ToArray())));

            if (!Peptide.IsNullOrEmpty())
                peptideConditions.Add(String.Format("pi.Peptide IN ({0})", String.Join(",", Peptide.Select(o => o.Id.ToString()).ToArray())));

            if (!DistinctMatchKey.IsNullOrEmpty())
                peptideConditions.Add(String.Format("IFNULL(dm.DistinctMatchKey, " +
                                                    DistinctMatchFormat.SqlExpression +
                                                    ") IN ('{0}')", String.Join("','", DistinctMatchKey.Select(o => o.Key).ToArray())));

            if (!ModifiedSite.IsNullOrEmpty())
                modConditions.Add(String.Format("pm.Site IN ('{0}')", String.Join("','", ModifiedSite.Select(o => o.ToString()).ToArray())));

            if (!Modifications.IsNullOrEmpty())
                modConditions.Add(String.Format("pm.Modification IN ({0})", String.Join(",", Modifications.Select(o => o.Id.ToString()).ToArray())));

            if (!Charge.IsNullOrEmpty())
                otherConditions.Add(String.Format("psm.Charge IN ({0})", String.Join(",", Charge.Select(o => o.ToString()).ToArray())));

            if (!Analysis.IsNullOrEmpty())
                otherConditions.Add(String.Format("psm.Analysis IN ({0})", String.Join(",", Analysis.Select(o => o.Id.ToString()).ToArray())));

            if (!Spectrum.IsNullOrEmpty())
                spectrumConditions.Add(String.Format("psm.Spectrum IN ({0})", String.Join(",", Spectrum.Select(o => o.Id.ToString()).ToArray())));

            if (!SpectrumSource.IsNullOrEmpty())
                spectrumConditions.Add(String.Format("s.Source IN ({0})", String.Join(",", SpectrumSource.Select(o => o.Id.ToString()).ToArray())));

            if (!SpectrumSourceGroup.IsNullOrEmpty())
                spectrumConditions.Add(String.Format("ssgl.Group_ IN ({0})", String.Join(",", SpectrumSourceGroup.Select(o => o.Id.ToString()).ToArray())));

            if (!AminoAcidOffset.IsNullOrEmpty())
            {
                var offsetConditions = new List<string>();
                foreach (int offset in AminoAcidOffset)
                {
                    if (offset <= 0)
                        offsetConditions.Add("pi.Offset = 0"); // protein N-terminus
                    else if (offset == Int32.MaxValue)
                        offsetConditions.Add("pi.Offset+pi.Length = pro.Length"); // protein C-terminus
                    else
                        offsetConditions.Add(String.Format("(pi.Offset <= {0} AND pi.Offset+pi.Length > {0})", offset));
                }

                otherConditions.Add("(" + String.Join(" OR ", offsetConditions.ToArray()) + ")");
            }

            var query = new StringBuilder();

            var conditions = new List<string>();
            if (proteinConditions.Count > 0) conditions.Add("(" + String.Join(" OR ", proteinConditions.ToArray()) + ")");
            if (peptideConditions.Count > 0) conditions.Add("(" + String.Join(" OR ", peptideConditions.ToArray()) + ")");
            if (spectrumConditions.Count > 0) conditions.Add("(" + String.Join(" OR ", spectrumConditions.ToArray()) + ")");
            if (modConditions.Count > 0) conditions.Add("(" + String.Join(" AND ", modConditions.ToArray()) + ")");
            if (otherConditions.Count > 0) conditions.Add("(" + String.Join(" AND ", otherConditions.ToArray()) + ")");

            if (conditions.Count > 0)
            {
                query.Append(" WHERE ");
                query.Append(String.Join(" AND ", conditions.ToArray()));
                query.Append(" ");
            }

            return query.ToString();
        }

        /// <summary>
        /// Calculates (by a greedy algorithm) how many additional results each protein group explains.
        /// </summary>
        static Map<long, long> CalculateAdditionalPeptides (NHibernate.ISession session)
        {
            var resultSetByProteinId = new Map<long, Set<Set<long>>>();
            var proteinGroupByProteinId = new Dictionary<long, int>();
            var proteinSetByProteinGroup = new Map<int, Set<long>>();
            var sharedResultsByProteinId = new Map<long, long>();

            session.CreateSQLQuery(@"DROP TABLE IF EXISTS SpectrumResults;
                                     CREATE TEMP TABLE SpectrumResults AS
                                     SELECT psm.Spectrum AS Spectrum, GROUP_CONCAT(DISTINCT psm.Peptide) AS Peptides, COUNT(DISTINCT pi.Protein) AS SharedResultCount
                                     FROM PeptideSpectrumMatch psm
                                     JOIN PeptideInstance pi ON psm.Peptide = pi.Peptide
                                     GROUP BY psm.Spectrum
                                    ").ExecuteUpdate();

            var queryByProtein = session.CreateSQLQuery(@"SELECT pro.Id, pro.ProteinGroup, SUM(sr.SharedResultCount)
                                                          FROM Protein pro
                                                          JOIN PeptideInstance pi ON pro.Id = pi.Protein
                                                          JOIN PeptideSpectrumMatch psm ON pi.Peptide = psm.Peptide
                                                          JOIN SpectrumResults sr ON psm.Spectrum = sr.Spectrum
                                                          WHERE pi.Id = (SELECT Id FROM PeptideInstance WHERE Peptide = pi.Peptide AND Protein = pi.Protein LIMIT 1)
                                                            AND psm.Id = (SELECT Id FROM PeptideSpectrumMatch WHERE Peptide = pi.Peptide LIMIT 1)
                                                          GROUP BY pro.Id");

            // For each protein, get the list of peptides evidencing it;
            // an ambiguous spectrum will show up as a nested list of peptides
            var queryByResult = session.CreateSQLQuery(@"SELECT pro.Id, GROUP_CONCAT(sr.Peptides)
                                                         FROM Protein pro
                                                         JOIN PeptideInstance pi ON pro.Id = pi.Protein
                                                         JOIN PeptideSpectrumMatch psm ON pi.Peptide = psm.Peptide
                                                         JOIN SpectrumResults sr ON psm.Spectrum = sr.Spectrum
                                                         WHERE pi.Id = (SELECT Id FROM PeptideInstance WHERE Peptide = pi.Peptide AND Protein = pi.Protein LIMIT 1)
                                                           AND psm.Id = (SELECT Id FROM PeptideSpectrumMatch WHERE Peptide = pi.Peptide LIMIT 1)
                                                         GROUP BY pro.Id, sr.Peptides");

            // keep track of the proteins that explain the most results
            Set<long> maxProteinIds = new Set<long>();
            int maxExplainedCount = 0;
            long minSharedResults = 0;

            foreach(var queryRow in queryByProtein.List<object[]>())
            {
                long proteinId = (long) queryRow[0];
                int proteinGroup = (int) queryRow[1];
                sharedResultsByProteinId[proteinId] = (long) queryRow[2];

                proteinGroupByProteinId[proteinId] = proteinGroup;
                proteinSetByProteinGroup[proteinGroup].Add(proteinId);
            }

            // construct the result set for each protein
            foreach (var queryRow in queryByResult.List<object[]>())
            {
                long proteinId = (long) queryRow[0];
                string resultIds = (string) queryRow[1];
                string[] resultIdTokens = resultIds.Split(',');
                Set<long> resultIdSet = new Set<long>(resultIdTokens.Select(o => Convert.ToInt64(o)));
                Set<Set<long>> explainedResults = resultSetByProteinId[proteinId];
                explainedResults.Add(resultIdSet);

                long sharedResults = sharedResultsByProteinId[proteinId];

                if (explainedResults.Count > maxExplainedCount)
                {
                    maxProteinIds.Clear();
                    maxProteinIds.Add(proteinId);
                    maxExplainedCount = explainedResults.Count;
                    minSharedResults = sharedResults;
                }
                else if (explainedResults.Count == maxExplainedCount)
                {
                    if (sharedResults < minSharedResults)
                    {
                        maxProteinIds.Clear();
                        maxProteinIds.Add(proteinId);
                        minSharedResults = sharedResults;
                    }
                    else if (sharedResults == minSharedResults)
                        maxProteinIds.Add(proteinId);
                }
            }

            var additionalPeptidesByProteinId = new Map<long, long>();

            // loop until the maxProteinIdsSetByProteinId map is empty
            while (resultSetByProteinId.Count > 0)
            {
                // the set of results explained by the max. proteins
                Set<Set<long>> maxExplainedResults = null;

                // remove max. proteins from the resultSetByProteinId map
                foreach (long maxProteinId in maxProteinIds)
                {
                    if (maxExplainedResults == null)
                        maxExplainedResults = resultSetByProteinId[maxProteinId];
                    else
                        maxExplainedResults.Union(resultSetByProteinId[maxProteinId]);

                    resultSetByProteinId.Remove(maxProteinId);
                    additionalPeptidesByProteinId[maxProteinId] = maxExplainedCount;
                }

                // subtract the max. proteins' results from the remaining proteins
                maxProteinIds.Clear();
                maxExplainedCount = 0;
                minSharedResults = 0;

                foreach (Map<long, Set<Set<long>>>.MapPair itr in resultSetByProteinId)
                {
                    Set<Set<long>> explainedResults = itr.Value;
                    explainedResults.Subtract(maxExplainedResults);

                    long sharedResults = sharedResultsByProteinId[itr.Key];

                    if (explainedResults.Count > maxExplainedCount)
                    {
                        maxProteinIds.Clear();
                        maxProteinIds.Add(itr.Key);
                        maxExplainedCount = explainedResults.Count;
                        minSharedResults = sharedResults;
                    }
                    else if (explainedResults.Count == maxExplainedCount)
                    {
                        if (sharedResults < minSharedResults)
                        {
                            maxProteinIds.Clear();
                            maxProteinIds.Add(itr.Key);
                            minSharedResults = sharedResults;
                        }
                        else if (sharedResults == minSharedResults)
                            maxProteinIds.Add(itr.Key);
                    }
                }

                // all remaining proteins present no additional evidence, so break the loop
                if (maxExplainedCount == 0)
                {
                    foreach (Map<long, Set<Set<long>>>.MapPair itr in resultSetByProteinId)
                        additionalPeptidesByProteinId[itr.Key] = 0;
                    break;
                }
            }

            return additionalPeptidesByProteinId;
        }

        Map<long, long> calculateProteinClusters (NHibernate.ISession session)
        {
            var spectrumSetByProteinId = new Map<long, Set<long>>();
            var proteinSetBySpectrumId = new Map<long, Set<long>>();

            var query = session.CreateQuery("SELECT pi.Protein.id, psm.Spectrum.id " +
                                            GetFilteredQueryString(FromProtein, ProteinToPeptideSpectrumMatch));

            foreach (var queryRow in query.List<object[]>())
            {
                long proteinId = (long) queryRow[0];
                long spectrumId = (long) queryRow[1];

                spectrumSetByProteinId[proteinId].Add(spectrumId);
                proteinSetBySpectrumId[spectrumId].Add(proteinId);
            }

            var clusterByProteinId = new Map<long, long>();
            int clusterId = 0;
            var clusterStack = new Stack<KeyValuePair<long, Set<long>>>();

            foreach (var pair in spectrumSetByProteinId)
            {
                long proteinId = pair.Key;

                if (clusterByProteinId.Contains(proteinId))
                    continue;

                // for each protein without a cluster assignment, make a new cluster
                ++clusterId;
                clusterStack.Push(new KeyValuePair<long, Set<long>>(proteinId, spectrumSetByProteinId[proteinId]));
                while (clusterStack.Count > 0)
                {
                    var kvp = clusterStack.Pop();

                    // try to assign the protein to the current cluster
                    var insertResult = clusterByProteinId.Insert(kvp.Key, clusterId);
                    if (!insertResult.WasInserted)
                        continue;

                    // add all "cousin" proteins to the current cluster
                    foreach (long spectrumId in kvp.Value)
                        foreach (var cousinProteinId in proteinSetBySpectrumId[spectrumId])
                            if (!clusterByProteinId.Contains(cousinProteinId))
                                clusterStack.Push(new KeyValuePair<long, Set<long>>(cousinProteinId, spectrumSetByProteinId[cousinProteinId]));
                }
            }

            return clusterByProteinId;
        }
    }

    /// <summary>
    /// Read-only wrapper of DataFilter that is safe to use as a Dictionary or Hashtable key.
    /// </summary>
    public class DataFilterKey
    {
        private DataFilter DataFilter { get; set; }

        public DataFilterKey (DataFilter dataFilter) { DataFilter = new DataFilter(dataFilter); }

        public override bool Equals (object obj)
        {
            DataFilterKey other = obj as DataFilterKey;
            if (other == null)
                return false;
            return DataFilter.Equals(other.DataFilter);
        }

        public override int GetHashCode ()
        {
            return DataFilter.MaximumQValue.GetHashCode() ^
                   DataFilter.MinimumDistinctPeptidesPerProtein.GetHashCode() ^
                   DataFilter.MinimumSpectraPerProtein.GetHashCode() ^
                   DataFilter.MinimumAdditionalPeptidesPerProtein.GetHashCode() ^
                   DataFilter.MinimumSpectraPerDistinctMatch.GetHashCode() ^
                   DataFilter.MinimumSpectraPerDistinctPeptide.GetHashCode() ^
                   DataFilter.MaximumProteinGroupsPerPeptide.GetHashCode() ^
                   NullSafeHashCode(DataFilter.Cluster) ^
                   NullSafeHashCode(DataFilter.ProteinGroup) ^
                   NullSafeHashCode(DataFilter.PeptideGroup) ^
                   NullSafeHashCode(DataFilter.Protein) ^
                   NullSafeHashCode(DataFilter.Peptide) ^
                   NullSafeHashCode(DataFilter.DistinctMatchKey) ^
                   NullSafeHashCode(DataFilter.Modifications) ^
                   NullSafeHashCode(DataFilter.ModifiedSite) ^
                   NullSafeHashCode(DataFilter.Charge) ^
                   NullSafeHashCode(DataFilter.Analysis) ^
                   NullSafeHashCode(DataFilter.Spectrum) ^
                   NullSafeHashCode(DataFilter.SpectrumSource) ^
                   NullSafeHashCode(DataFilter.SpectrumSourceGroup) ^
                   NullSafeHashCode(DataFilter.AminoAcidOffset) ^
                   NullSafeHashCode(DataFilter.Composition);
        }

        private static int NullSafeHashCode<T> (IEnumerable<T> obj)
        {
            if (obj == null)
                return 0;

            int hash = 0;
            foreach(T item in obj)
                hash ^= item.GetHashCode();
            return hash;
        }
    }
}