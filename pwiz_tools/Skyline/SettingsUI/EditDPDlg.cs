﻿/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Windows.Forms;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class EditDPDlg : Form
    {
        private DeclusteringPotentialRegression _regression;
        private readonly IEnumerable<DeclusteringPotentialRegression> _existing;

        public EditDPDlg(IEnumerable<DeclusteringPotentialRegression> existing)
        {
            _existing = existing;

            InitializeComponent();

            var document = Program.ActiveDocumentUI;
            btnUseCurrent.Enabled = document.Settings.HasResults &&
                                    document.Settings.MeasuredResults.Chromatograms.Contains(
                                        chrom => chrom.OptimizationFunction is DeclusteringPotentialRegression);
        }

        public DeclusteringPotentialRegression Regression
        {
            get { return _regression; }
            
            set
            {
                _regression = value;
                if (_regression == null)
                {
                    textName.Text = "";
                    textSlope.Text = "";
                    textIntercept.Text = "";
                    textStepSize.Text = "";
                    textStepCount.Text = "";
                }
                else
                {
                    textName.Text = _regression.Name;
                    textSlope.Text = _regression.Slope.ToString();
                    textIntercept.Text = _regression.Intercept.ToString();
                    textStepSize.Text = _regression.StepSize.ToString();
                    textStepCount.Text = _regression.StepCount.ToString();
                }                
            }
        }

        public void OkDialog()
        {
            // TODO: Remove this
            var e = new CancelEventArgs();
            var helper = new MessageBoxHelper(this);

            string name;
            if (!helper.ValidateNameTextBox(e, textName, out name))
                return;

            if (_regression == null && _existing.Contains(r => Equals(name, r.Name)))
            {
                helper.ShowTextBoxError(textName, "The retention time regression '{0}' already exists.", name);
                return;
            }

            double slope;
            if (!helper.ValidateDecimalTextBox(e, textSlope, out slope))
                return;

            double intercept;
            if (!helper.ValidateDecimalTextBox(e, textIntercept, out intercept))
                return;

            double stepSize;
            if (!helper.ValidateDecimalTextBox(e, textStepSize,
                    DeclusteringPotentialRegression.MIN_STEP_SIZE,
                    DeclusteringPotentialRegression.MAX_STEP_SIZE,
                    out stepSize))
                return;

            int stepCount;
            if (!helper.ValidateNumberTextBox(e, textStepCount,
                    OptimizableRegression.MIN_OPT_STEP_COUNT,
                    OptimizableRegression.MAX_OPT_STEP_COUNT,
                    out stepCount))
                return;

            _regression = new DeclusteringPotentialRegression(name, slope, intercept, stepSize, stepCount);

            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void btnUseCurrent_Click(object sender, EventArgs e)
        {

        }
    }
}
