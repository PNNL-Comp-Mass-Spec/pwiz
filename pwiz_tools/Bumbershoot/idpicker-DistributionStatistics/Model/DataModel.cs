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
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Data;
using NHibernate;
using NHibernate.Linq;
using Iesi.Collections.Generic;
using msdata = pwiz.CLI.msdata;

namespace IDPicker.DataModel
{
    public class SpectrumSourceGroup : Entity<SpectrumSourceGroup>
    {
        public virtual string Name { get; set; }
        public virtual Iesi.Collections.Generic.ISet<SpectrumSourceGroupLink> Sources { get; set; }
    }

    public class SpectrumSource : Entity<SpectrumSource>
    {
        /// <summary>
        /// The direct parent group of this spectrum source.
        /// </summary>
        public virtual SpectrumSourceGroup Group { get; set; }

        /// <summary>
        /// All parent groups of this spectrum source (direct and indirect).
        /// </summary>
        public virtual Iesi.Collections.Generic.ISet<SpectrumSourceGroupLink> Groups { get; set; }
        public virtual string Name { get; set; }
        public virtual string URL { get; set; }
        public virtual IList<Spectrum> Spectra { get; set; }

        public virtual msdata.MSData Metadata { get; set; }

        #region Transient instance members
        public SpectrumSource () { }
        public SpectrumSource (pwiz.CLI.msdata.MSData msd)
        {
            Metadata = msd;
        }
        #endregion
    }

    // HACK: explicit many-to-many linker table is needed for
    // fast queries on indirect relationships between sources and groups
    // i.e. group '/' has a link to ALL sources
    public class SpectrumSourceGroupLink : Entity<SpectrumSourceGroupLink>
    {
        public virtual SpectrumSourceGroup Group { get; set; }
        public virtual SpectrumSource Source { get; set; }
    }

    public class Spectrum : Entity<Spectrum>
    {
        public virtual SpectrumSource Source { get; set; }
        public virtual int Index { get; set; }
        public virtual string NativeID { get; set; }
        public virtual double PrecursorMZ { get; set; }
        public virtual IList<PeptideSpectrumMatch> Matches { get; set; }

        public virtual msdata.Spectrum Metadata
        {
            get
            {
                var spectrumList = Source.Metadata.run.spectrumList;
                return spectrumList.spectrum(spectrumList.find(NativeID), false);
            }
        }

        public virtual msdata.Spectrum MetadataWithPeaks
        {
            get
            {
                var spectrumList = Source.Metadata.run.spectrumList;
                return spectrumList.spectrum(spectrumList.find(NativeID), true);
            }
        }
    }

    public class AnalysisSoftware
    {
        public string Name { get; set; }
        public string Version { get; set; }
    }

    public enum AnalysisType
    {
        DatabaseSearch,
        SpectrumLibrary,
        SequenceTag
    }

    public class AnalysisParameter : Entity<AnalysisParameter>, IComparable<AnalysisParameter>
    {
        public virtual Analysis Analysis { get; set; }
        public virtual string Name { get; set; }
        public virtual string Value { get; set; }

        public virtual int CompareTo (AnalysisParameter other)
        {
            int nameCompare = Name.CompareTo(other.Name);
            if (nameCompare == 0)
                return Value.CompareTo(other.Value);
            return nameCompare;
        }
    }

    public class Analysis : Entity<Analysis>
    {
        public virtual string Name { get; set; }
        public virtual AnalysisSoftware Software { get; set; }
        public virtual AnalysisType Type { get; set; }
        public virtual DateTime? StartTime { get; set; }
        public virtual QonverterSettings QonverterSettings { get; set; }
        public virtual Iesi.Collections.Generic.ISet<AnalysisParameter> Parameters { get; set; }
        public virtual IList<PeptideSpectrumMatch> Matches { get; set; }
    }

    public class Protein : Entity<Protein>
    {
        public virtual string Accession { get; set; }
        public virtual string Description { get; set; }
        public virtual string Sequence { get; set; }
        public virtual IList<PeptideInstance> Peptides { get; set; }

        public virtual bool IsDecoy { get { return isDecoy; } }
        public virtual int Cluster { get { return cluster; } }
        public virtual int ProteinGroup { get { return proteinGroup; } }
        public virtual int Length { get { return length; } }

        public virtual double Coverage { get; set; }
        public virtual IList<ushort> CoverageMask { get; set; }

        #region Transient instance members
        public Protein () { }
        public Protein (string description, string sequence)
        {
            Description = description;
            Sequence = sequence;
            this.length = sequence.Length;
        }

        bool isDecoy = false;
        int cluster = 0;
        int proteinGroup = 0;
        int length = 0;

        #endregion
    }

    public class Peptide : Entity<Peptide>
    {
        public virtual string Sequence { get { return sequence; } }

        public virtual IList<PeptideInstance> Instances { get; set; }
        public virtual IList<PeptideSpectrumMatch> Matches { get; set; }

        public virtual double MonoisotopicMass { get; set; }
        public virtual double MolecularWeight { get; set; }
        public virtual int PeptideGroup { get { return peptideGroup; } }

        #region Transient instance members
        public Peptide () { }
        internal Peptide (string sequence)
        {
            this.sequence = sequence;
        }

        string sequence = null;
        int peptideGroup = 0;
        string DecoySequence { get; set; }
        #endregion
    }

    public class PeptideInstance : Entity<PeptideInstance>
    {
        public virtual Peptide Peptide { get; set; }
        public virtual Protein Protein { get; set; }

        /// <summary>
        /// The zero-based offset in the protein where this peptide instance occurs.
        /// </summary>
        public virtual int Offset { get; set; }

        public virtual int Length { get; set; }
        public virtual bool NTerminusIsSpecific { get; set; }
        public virtual bool CTerminusIsSpecific { get; set; }
        public virtual int MissedCleavages { get; set; }

        private int specificTermini = -1;
        public virtual int SpecificTermini { get { return specificTermini; } }
    }

    public class Modification : Entity<Modification>
    {
        public virtual double MonoMassDelta { get; set; }
        public virtual double AvgMassDelta { get; set; }
        public virtual string Formula { get; set; }
        public virtual string Name { get; set; }
    }

    public class PeptideSpectrumMatch : Entity<PeptideSpectrumMatch>
    {
        public virtual Spectrum Spectrum { get; set; }
        public virtual Analysis Analysis { get; set; }
        public virtual Peptide Peptide { get; set; }
        public virtual IList<PeptideModification> Modifications { get; set; }
        public virtual double QValue { get; set; }
        public virtual double ObservedNeutralMass { get; set; }
        public virtual double MonoisotopicMassError { get; set; }
        public virtual double MolecularWeightError { get; set; }
        public virtual int Rank { get; set; }
        public virtual int Charge { get; set; }
        public virtual IDictionary<string, double> Scores { get; set; }

        public virtual string DistinctMatchKey { get { return distinctMatchKey; } }

        /// <summary>
        /// Valid Q values are always between 0 and 1;
        /// '2' is a better invalid Q value than Double.MaxValue or Double.PositiveInfinity
        /// because '1.79769313486232E308' is annoyingly long and 'Infinity' doesn't fly with SQLite.
        /// </summary>
        public readonly static double DefaultQValue = 2;

        #region Transient instance members
        public PeptideSpectrumMatch () { QValue = DefaultQValue; }
        public PeptideSpectrumMatch (PeptideSpectrumMatch other)
        {
            Id = other.Id;
            Spectrum = other.Spectrum;
            Analysis = other.Analysis;
            Peptide = other.Peptide;
            Modifications = other.Modifications;
            QValue = other.QValue;
            ObservedNeutralMass = other.ObservedNeutralMass;
            MonoisotopicMassError = other.MonoisotopicMassError;
            MolecularWeightError = other.MolecularWeightError;
            Rank = other.Rank;
            Charge = other.Charge;
            Scores = other.Scores;
        }

        private string distinctMatchKey = "";
        #endregion
    }

    public class PeptideModification : Entity<PeptideModification>
    {
        public virtual PeptideSpectrumMatch PeptideSpectrumMatch { get; set; }
        public virtual Modification Modification { get; set; }

        /// <summary>
        /// The zero-based location of the modification in the peptide.
        /// </summary>
        public virtual int Offset
        {
            get { return offset; }
            set
            {
                offset = value;

                if (offset == int.MinValue)
                    site = '(';
                else if (offset == int.MaxValue)
                    site = ')';
                else
                {
                    var firstInstance = PeptideSpectrumMatch.Peptide.Instances.First();
                    site = firstInstance.Protein.Sequence[firstInstance.Offset + offset];
                }
            }
        }

        /// <summary>
        /// A symbol representing the modified site, taking one of the following forms:
        /// <para>- an amino acid symbol for a non-terminal modification</para>
        /// <para>- '(' for an N-terminal modification</para>
        /// <para>- ')' for a C-terminal modification</para>
        /// </summary>
        public virtual char Site { get { return site; } }

        #region Transient instance members
        public PeptideModification () { }
        internal PeptideModification (string sequence, int offset)
        {
            this.offset = offset;

            if (offset == int.MinValue)
                site = '(';
            else if (offset == int.MaxValue)
                site = ')';
            else
                site = sequence[offset];
        }

        private int offset;
        private char site;
        #endregion
    }

    public class Entity<T> : IEquatable<T> where T : Entity<T>, new()
    {
        public virtual long? Id { get; set; }
        protected virtual int Version { get; set; }

        public override bool Equals (Object o)
        {
            if (Id == null)
            {
                return base.Equals(o);
            }
            Entity<T> that = o as Entity<T>;
            if (that == null)
            {
                return false;
            }
            return Equals(Id, that.Id);
        }

        public virtual bool Equals (T o) { return Equals((object) o); }

        public override int GetHashCode ()
        {
            if (Id == null)
            {
                return base.GetHashCode();
            }
            return Id.GetHashCode() ^ typeof(T).GetHashCode();
        }
    }

    #region Implementation for custom types

    public class AminoAcidSequence
    {
        private ushort[] _encodedSequence;
        private int _sequenceLength;

        public AminoAcidSequence (string sequence)
        {
            _encodedSequence = new ushort[(int) Math.Ceiling(sequence.Length / 3.0)];
            _sequenceLength = sequence.Length;

            for (int i = 0; i + 2 < sequence.Length; i += 3)
            {
                ushort encodedTrimer = (ushort) (((sequence[i] - 'A') << 10) +
                                                ((sequence[i+1] - 'A') << 5) +
                                                (sequence[i+2] - 'A'));
                _encodedSequence[i/3] = encodedTrimer;
            }

            int mod = sequence.Length % 3;
            if (mod == 2)
                _encodedSequence[_encodedSequence.Length-1] = (ushort) (((sequence[sequence.Length - 2] - 'A') << 5) +
                                                        (sequence[sequence.Length - 1] - 'A'));
            else if (mod == 1)
                _encodedSequence[_encodedSequence.Length-1] = (ushort) (sequence[sequence.Length - 1] - 'A');
        }

        public override string ToString ()
        {
            var sequence = new StringBuilder(_sequenceLength);
            for (int i = 0; i + 2 < _sequenceLength; i += 3)
            {
                int offset = i * 3;
                sequence.Append((char) ((_encodedSequence[i / 3] >> 10) + 'A'));
                sequence.Append((char) (((_encodedSequence[i / 3] >> 5) & 0x1F) + 'A'));
                sequence.Append((char) ((_encodedSequence[i / 3] & 0x1F) + 'A'));
            }

            int mod = _sequenceLength % 3;
            if (mod == 2)
            {
                sequence.Append((char) (((_encodedSequence.Last() >> 5) & 0x1F) + 'A'));
                sequence.Append((char) ((_encodedSequence.Last() & 0x1F) + 'A'));
            }
            else if (mod == 1)
                sequence.Append((char) (_encodedSequence.Last() + 'A'));

            return sequence.ToString();
        }
    }

    public class TemporaryMSDataFile : msdata.MSDataFile, IDisposable
    {
        public TemporaryMSDataFile(string tempFilepath) : base(tempFilepath)
        {
            Filepath = tempFilepath;
        }

        public new void Dispose ()
        {
            try
            {
                base.Dispose();
                File.Delete(Filepath);
            }
            catch (Exception)
            {
                // TODO: log
            }
        }

        public string Filepath { get; private set; }
    }

    public class SpectrumSourceMetadataUserType : NHibernate.UserTypes.IUserType
    {
        #region IUserType Members

        public object Assemble (object cached, object owner)
        {
            if (cached == null)
                return null;

            if (cached == DBNull.Value)
                return null;

            if (!(cached is byte[]))
                throw new ArgumentException();
            
            string tempFilepath = Path.GetTempFileName();
            File.Move(tempFilepath, tempFilepath + ".mz5");
            tempFilepath = tempFilepath + ".mz5";

            var msdataBytes = cached as byte[];
            File.WriteAllBytes(tempFilepath, msdataBytes);

            return new TemporaryMSDataFile(tempFilepath);
        }

        public object DeepCopy (object value)
        {
            if (value == null)
                return null;
            var msd = value as msdata.MSData;
            return msd;
        }

        public object Disassemble (object value)
        {
            if (value == null)
                return DBNull.Value;

            if (value == DBNull.Value)
                return DBNull.Value;

            if (!(value is msdata.MSData))
                throw new ArgumentException();

            string tempFilepath = Path.GetTempFileName();
            File.Move(tempFilepath, tempFilepath + ".mz5");
            tempFilepath = tempFilepath + ".mz5";

            var msd = value as msdata.MSData;
            msdata.MSDataFile.write(msd, tempFilepath,
                                    new msdata.MSDataFile.WriteConfig() { gzipped = true });
            byte[] msdataBytes = File.ReadAllBytes(tempFilepath);
            File.Delete(tempFilepath);
            return msdataBytes;
        }

        public int GetHashCode (object x)
        {
            return x.GetHashCode();
        }

        public object NullSafeGet (IDataReader rs, string[] names, object owner)
        {
            return Assemble(rs.GetValue(0), owner);
        }

        public void NullSafeSet (IDbCommand cmd, object value, int index)
        {
            (cmd.Parameters[index] as IDataParameter).Value = Disassemble(value);
        }

        public object Replace (object original, object target, object owner)
        {
            throw new NotImplementedException();
        }

        public Type ReturnedType
        {
            get { return typeof(msdata.MSData); }
        }

        public NHibernate.SqlTypes.SqlType[] SqlTypes
        {
            get { return new NHibernate.SqlTypes.SqlType[] { NHibernate.SqlTypes.SqlTypeFactory.GetBinaryBlob(1) }; }
        }

        public bool IsMutable
        {
            get { return true; }
        }

        bool NHibernate.UserTypes.IUserType.Equals (object x, object y)
        {
            if (x == null && y == null)
                return true;
            else if (x == null || y == null)
                return false;
            var msd1 = x as msdata.MSData;
            var msd2 = y as msdata.MSData;
            return msd1.Equals(msd2);
        }

        #endregion
    }

    public class ProteinCoverageMaskUserType : NHibernate.UserTypes.IUserType
    {
        #region IUserType Members

        public object Assemble (object cached, object owner)
        {
            if (cached == null)
                return null;

            if (cached == DBNull.Value)
                return null;

            if (!(cached is byte[]))
                throw new ArgumentException();

            var bytes = cached as byte[];

            if(bytes.Length < sizeof(int) + sizeof(ushort))
                return null;

            var mask = new List<ushort>();
            using (var stream = new BinaryReader(new MemoryStream(bytes)))
            {
                int length = stream.ReadInt32();
                for (int i = 0; i < length; ++i)
                    mask.Add(stream.ReadUInt16());
            }
            return mask;
        }

        public object DeepCopy (object value)
        {
            if (value == null)
                return null;
            return value as IList<ushort>;
        }

        public object Disassemble (object value)
        {
            if (value == null)
                return DBNull.Value;

            if (value == DBNull.Value)
                return DBNull.Value;

            if (!(value is IList<ushort>))
                throw new ArgumentException();

            var mask = value as IList<ushort>;

            var bytes = new byte[sizeof(int) + sizeof(ushort) * mask.Count];
            using (var stream = new BinaryWriter(new MemoryStream(bytes, true)))
            {
                stream.Write(mask.Count);
                foreach (ushort i in mask)
                    stream.Write(i);
            }
            return bytes;
        }

        public int GetHashCode (object x)
        {
            return x.GetHashCode();
        }

        public object NullSafeGet (IDataReader rs, string[] names, object owner)
        {
            return Assemble(rs.GetValue(rs.GetOrdinal(names[0])), owner);
        }

        public void NullSafeSet (IDbCommand cmd, object value, int index)
        {
            (cmd.Parameters[index] as IDataParameter).Value = Disassemble(value);
        }

        public object Replace (object original, object target, object owner)
        {
            throw new NotImplementedException();
        }

        public Type ReturnedType
        {
            get { return typeof(IList<ushort>); }
        }

        public NHibernate.SqlTypes.SqlType[] SqlTypes
        {
            get { return new NHibernate.SqlTypes.SqlType[] { NHibernate.SqlTypes.SqlTypeFactory.GetBinaryBlob(1) }; }
        }

        public bool IsMutable
        {
            get { return false; }
        }

        bool NHibernate.UserTypes.IUserType.Equals (object x, object y)
        {
            if (x == null && y == null)
                return true;
            else if (x == null || y == null)
                return false;
            var mask1 = x as IList<ushort>;
            var mask2 = y as IList<ushort>;
            if(mask1.Count != mask2.Count)
                return false;
            return mask1.SequenceEqual(mask2);
        }

        #endregion
    }
    #endregion

    public static class ExtensionMethods
    {
        /// <summary>
        /// Gets a single result from a query on the session matching the given expression.
        /// If the query returns more than one result, returns null.
        /// </summary>
        public static T UniqueResult<T> (this ISession session, System.Linq.Expressions.Expression<Func<T, bool>> expression) where T : class
        {
            return session.Query<T>().Where(expression).SingleOrDefault();
        }

        /// <summary>
        /// Gets a single result from a query on the session matching the given expression.
        /// If the query returns more than one result, returns null.
        /// </summary>
        public static T UniqueResult<T> (this ISession session, string hql) where T : class
        {
            return session.CreateQuery(hql).UniqueResult<T>();
        }

        public static string ToModifiedString (this PeptideSpectrumMatch psm, int precision)
        {
            string format = String.Format("[{{0:f{0}}}]", precision);
            StringBuilder sb = new StringBuilder(psm.Peptide.Sequence);
            foreach (var mod in (from m in psm.Modifications orderby m.Offset descending select m))
                if (mod.Offset == int.MinValue)
                    sb.Insert(0, String.Format(format, mod.Modification.MonoMassDelta));
                else if (mod.Offset == int.MaxValue)
                    sb.AppendFormat(format, mod.Modification.MonoMassDelta);
                else
                    sb.Insert(mod.Offset + 1, String.Format(format, mod.Modification.MonoMassDelta));
            return sb.ToString();
        }

        public static string ToModifiedString (this PeptideSpectrumMatch psm)
        {
            return ToModifiedString(psm, 0);
        }

        public static int GetGroupDepth (this SpectrumSourceGroup group)
        {
            if (group.Name == "/")
                return 0;

            int count = 0;
            foreach (char c in group.Name)
                if (c == '/')
                    ++count;
            return count;
        }

        public static bool IsChildOf (this SpectrumSourceGroup childGroup, SpectrumSourceGroup parentGroup)
        {
            return childGroup.Name.StartsWith(parentGroup.Name == "/" ? "/" : (parentGroup.Name + "/"));
        }

        public static bool IsImmediateChildOf (this SpectrumSourceGroup childGroup, SpectrumSourceGroup parentGroup)
        {
            return childGroup.IsChildOf(parentGroup) &&
                   childGroup.GetGroupDepth() - 1 == parentGroup.GetGroupDepth();
        }

        public static string ToProteinIdList (this IList<PeptideInstance> instances)
        {
            return String.Format("({0})", String.Join(",", (from pi in instances select pi.Protein.Id.ToString()).ToArray()));
        }
    }
}