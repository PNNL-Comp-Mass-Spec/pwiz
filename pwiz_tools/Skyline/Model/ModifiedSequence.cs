﻿/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    /// <summary>
    /// Holds an unmodified sequence and a list of explicit modifications (i.e. StatidMod and AminoAcid index).
    /// This enables the sequence to be formatted in a number of ways, including mass deltas, or modification names.
    /// </summary>
    public class ModifiedSequence
    {
        public const string UnimodPrefix = "unimod:"; // Not L10N

        /// <summary>
        /// Constructs a ModifiedSequence from SrmSettings and PeptideDocNode.
        /// </summary>
        public static ModifiedSequence GetModifiedSequence(SrmSettings settings, PeptideDocNode docNode, IsotopeLabelType labelType)
        {
            if (docNode.Peptide.IsCustomMolecule)
            {
                return null;
            }
            var unmodifiedSequence = docNode.Peptide.Sequence;
            bool includeStaticMods = true;
            List<Modification> explicitMods = new List<Modification>();
            if (null != docNode.ExplicitMods)
            {
                var labelMods = docNode.ExplicitMods.GetModifications(labelType);
                if (labelMods != null)
                {
                    var monoMasses = docNode.ExplicitMods.GetModMasses(MassType.Monoisotopic, labelType);
                    var avgMasses = docNode.ExplicitMods.GetModMasses(MassType.Average, labelType);
                    foreach (var mod in labelMods)
                    {
                        explicitMods.Add(new Modification(mod, monoMasses[mod.IndexAA], avgMasses[mod.IndexAA]));
                    }
                    includeStaticMods = docNode.ExplicitMods.IsVariableStaticMods;
                }
            }

            if (includeStaticMods)
            {
                var peptideModifications = settings.PeptideSettings.Modifications;
                for (int i = 0; i < unmodifiedSequence.Length; i++)
                {
                    foreach (var staticMod in peptideModifications.GetModifications(labelType))
                    {
                        if (staticMod.IsExplicit || staticMod.IsVariable)
                        {
                            continue;
                        }
                        if (staticMod.Terminus.HasValue)
                        {
                            if (staticMod.Terminus == ModTerminus.N && i != 0)
                            {
                                continue;
                            }
                            if (staticMod.Terminus == ModTerminus.C && i != unmodifiedSequence.Length - 1)
                            {
                                continue;
                            }
                        }
                        if (!string.IsNullOrEmpty(staticMod.AAs) && !staticMod.AAs.Contains(unmodifiedSequence[i]))
                        {
                            continue;
                        }
                        var monoMass = staticMod.MonoisotopicMass ??
                                       SrmSettings.MonoisotopicMassCalc.GetAAModMass(unmodifiedSequence[i], i,
                                           unmodifiedSequence.Length);
                        var avgMass = staticMod.AverageMass ??
                                      SrmSettings.AverageMassCalc.GetAAModMass(unmodifiedSequence[i], i,
                                          unmodifiedSequence.Length);
                        explicitMods.Add(new Modification(new ExplicitMod(i, staticMod), monoMass, avgMass));
                    }
                }
            }
            return new ModifiedSequence(unmodifiedSequence, explicitMods, settings.TransitionSettings.Prediction.PrecursorMassType);
        }
        
        private readonly string _unmodifiedSequence;
        private readonly ImmutableList<Modification> _explicitMods;
        private readonly MassType _defaultMassType;
        public ModifiedSequence(string unmodifiedSequence, IEnumerable<Modification> explicitMods, MassType defaultMassType)
        {
            _unmodifiedSequence = unmodifiedSequence;
            _explicitMods = ImmutableList.ValueOf(explicitMods.OrderBy(mod=>mod.IndexAA, SortOrder.Ascending));
            _defaultMassType = defaultMassType;
        }

       public string MonoisotopicMasses
        {
            get { return Format(mods => FormatMassModification(mods, MassType.Monoisotopic)); }
        }

        public string AverageMasses
        {
            get { return Format(mods => FormatMassModification(mods, MassType.Average)); }
        }

        public string ThreeLetterCodes
        {
            get { return FormatModsIndividually(FormatThreeLetterCode); }
        }

        public string FullNames
        {
            get { return FormatModsIndividually(FormatFullName); }
        }

        public string UnimodIds
        {
            get { return Format(FormatUnimodIds); }
        }

        public override string ToString()
        {
            return Format(mods=>FormatMassModification(mods, _defaultMassType));
        }

        private string FormatModsIndividually(Func<Modification, string> modFormatter)
        {
            return Format(mods =>string.Join(string.Empty, mods.Select(mod => Bracket(modFormatter(mod)))));
        }

        private string Format(Func<IEnumerable<Modification>, string> modFormatter)
        {
            StringBuilder result = new StringBuilder();
            int seqCharsReturned = 0;
            foreach (var modGroup in _explicitMods.GroupBy(mod=>mod.IndexAA))
            {
                result.Append(_unmodifiedSequence.Substring(seqCharsReturned,
                    modGroup.Key + 1 - seqCharsReturned));
                seqCharsReturned = modGroup.Key + 1;
                result.Append(modFormatter(modGroup.ToArray()));
            }
            result.Append(_unmodifiedSequence.Substring(seqCharsReturned));
            return result.ToString();
        }

        public static string FormatThreeLetterCode(Modification modification)
        {
            if (string.IsNullOrEmpty(modification.ShortName))
            {
                return FormatFullName(modification);
            }
            return modification.ShortName;
        }

        public static string FormatMassModification(IEnumerable<Modification> mods, MassType massType)
        {
            double mass = 0;
            foreach (var mod in mods)
            {
                double? modMass = (massType & MassType.Average) != 0 ? mod.AverageMass : mod.MonoisotopicMass;
                mass += modMass.GetValueOrDefault();
            }
            if (mass == 0)
            {
                return String.Empty;
            }
            string strMod = Math.Round(mass, 1).ToString(CultureInfo.InvariantCulture);
            if (mass > 0)
            {
                strMod = "+" + strMod; // Not L10N
            }
            return Bracket(strMod);
        }


        public static string FormatFullName(Modification mod)
        {
            if (string.IsNullOrEmpty(mod.Name))
            {
                return FormatFallback(mod);
            }
            return mod.Name;
        }

        public static string FormatUnimodIds(IEnumerable<Modification> mods)
        {
            return string.Join(String.Empty, mods.Select(mod =>
            {
                if (mod.UnimodId.HasValue)
                {
                    return "(" + UnimodPrefix + mod.UnimodId.Value + ")";  // Not L10N
                }
                return Bracket(FormatFallback(mod));
            }));
        }

        public static string FormatFallback(IEnumerable<Modification> mods)
        {
            return string.Join(string.Empty, mods.Select(mod => Bracket(FormatFallback(mod))));
        }

        /// <summary>
        /// Converts the modification to a string in the safest way possible. This is used
        /// when the requested modification format (e.g. Three Letter Code) is not available
        /// for this modification.
        /// </summary>
        public static string FormatFallback(Modification mod)
        {
            if (!string.IsNullOrEmpty(mod.Name))
            {
                return mod.Name;
            }
            if (!string.IsNullOrEmpty(mod.ShortName))
            {
                return mod.ShortName;
            }
            if (mod.MonoisotopicMass != 0)
            {
                return mod.MonoisotopicMass.ToString(CultureInfo.CurrentCulture);
            }
            if (!string.IsNullOrEmpty(mod.Formula))
            {
                return mod.Formula;
            }
            return "#UNKNOWNMODIFICATION#"; // Not L10N
        }

        /// <summary>
        /// Adds brackets around a modification. If the modification itself contains the close
        /// bracket character, then uses either parentheses or braces.
        /// </summary>
        public static string Bracket(string str)
        {
            // ReSharper disable NonLocalizedString
            if (!str.Contains("]")) 
            {
                return "[" + str + "]";
            }
            if (!str.Contains(")"))
            {
                return "(" + str + ")";
            }
            if (!str.Contains("}"))
            {
                return "{" + str + "}";
            }
            // We could not find a safe type of bracket to use.
            // Just replace all the close brackets with underscores.
            return "[" + str.Replace("]", "_") + "]";
            // ReSharper restore NonLocalizedString
        }

        public class Modification
        {
            public Modification(ExplicitMod explicitMod, double monoMass, double avgMass)
            {
                ExplicitMod = explicitMod;
                MonoisotopicMass = monoMass;
                AverageMass = avgMass;
            }

            public ExplicitMod ExplicitMod { get; private set; }
            public StaticMod StaticMod { get { return ExplicitMod.Modification; } }
            public int IndexAA {get { return ExplicitMod.IndexAA; } }
            public string Name { get { return StaticMod.Name; } }
            public string ShortName { get { return StaticMod.ShortName; } }
            public string Formula { get { return StaticMod.Formula; } }
            public int? UnimodId { get { return StaticMod.UnimodId; } }
            public double MonoisotopicMass { get; private set; }
            public double AverageMass { get; private set; }
        }
    }
}
