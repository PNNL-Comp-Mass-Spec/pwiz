﻿/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Properties;
using SkylineTool;

namespace pwiz.Skyline.Model.Databinding
{
    public class SkylineDataSchema : DataSchema
    {
        private readonly IDocumentContainer _documentContainer;
        private readonly HashSet<IDocumentChangeListener> _documentChangedEventHandlers 
            = new HashSet<IDocumentChangeListener>();
        private readonly CachedValue<ImmutableSortedList<ResultKey, Replicate>> _replicates;
        private readonly CachedValue<IDictionary<ResultFileKey, ResultFile>> _resultFiles;

        private SrmDocument _batchChangesOriginalDocument;

        private SrmDocument _document;
        public SkylineDataSchema(IDocumentContainer documentContainer, DataSchemaLocalizer dataSchemaLocalizer) : base(dataSchemaLocalizer)
        {
            _documentContainer = documentContainer;
            _document = _documentContainer.Document;
            ChromDataCache = new ChromDataCache();
            _replicates = CachedValue.Create(this, CreateReplicateList);
            _resultFiles = CachedValue.Create(this, CreateResultFileList);
        }

        protected override bool IsScalar(Type type)
        {
            return base.IsScalar(type) || type == typeof(IsotopeLabelType) || type == typeof(DocumentLocation) ||
                   type == typeof(SampleType) || type == typeof(GroupIdentifier) || type == typeof(StandardType) ||
                   type == typeof(NormalizationMethod);
        }

        public override bool IsRootTypeSelectable(Type type)
        {
            return base.IsRootTypeSelectable(type) && type != typeof(SkylineDocument);
        }

        public override IEnumerable<PropertyDescriptor> GetPropertyDescriptors(Type type)
        {
            return base.GetPropertyDescriptors(type).Concat(GetAnnotations(type)).Concat(GetRatioProperties(type));
        }

        public IEnumerable<AnnotationPropertyDescriptor> GetAnnotations(Type type)
        {
            if (null == type)
            {
                return new AnnotationPropertyDescriptor[0];
            }
            var annotationTargets = GetAnnotationTargets(type);
            if (annotationTargets.IsEmpty)
            {
                return new AnnotationPropertyDescriptor[0];
            }
            var properties = new List<AnnotationPropertyDescriptor>();
            foreach (var annotationDef in Document.Settings.DataSettings.AnnotationDefs)
            {
                if (annotationDef.AnnotationTargets.Intersect(annotationTargets).IsEmpty)
                {
                    continue;
                }
                properties.Add(new AnnotationPropertyDescriptor(annotationDef, true));
            }
            return properties;
        }

        private AnnotationDef.AnnotationTargetSet GetAnnotationTargets(Type type)
        {
            return AnnotationDef.AnnotationTargetSet.OfValues(
                type.GetCustomAttributes(true)
                    .OfType<AnnotationTargetAttribute>()
                    .Select(attr => attr.AnnotationTarget));
        }

        public IEnumerable<RatioPropertyDescriptor> GetRatioProperties(Type type)
        {
            return RatioPropertyDescriptor.ListProperties(Document, type);
        }

        public SrmDocument Document
        {
            get
            {
                return _document;
            }
        }
        public void Listen(IDocumentChangeListener listener)
        {
            lock (_documentChangedEventHandlers)
            {
                bool firstListener = _documentChangedEventHandlers.Count == 0;
                if (!_documentChangedEventHandlers.Add(listener))
                {
                    throw new ArgumentException("Listener already added"); // Not L10N
                }
                if (firstListener)
                {
                    var documentUiContainer = _documentContainer as IDocumentUIContainer;
                    if (null == documentUiContainer)
                    {
                        _documentContainer.Listen(DocumentChangedEventHandler);
                    }
                    else
                    {
                        documentUiContainer.ListenUI(DocumentChangedEventHandler);
                    }
                }
            }
        }

        public void Unlisten(IDocumentChangeListener listener)
        {
            lock (_documentChangedEventHandlers)
            {
                if (!_documentChangedEventHandlers.Remove(listener))
                {
                    throw new ArgumentException("Listener not added"); // Not L10N
                }
                if (_documentChangedEventHandlers.Count == 0)
                {
                    var documentUiContainer = _documentContainer as IDocumentUIContainer;
                    if (null == documentUiContainer)
                    {
                        _documentContainer.Unlisten(DocumentChangedEventHandler);
                    }
                    else
                    {
                        documentUiContainer.UnlistenUI(DocumentChangedEventHandler);
                    }
                }
            }
        }

        private void DocumentChangedEventHandler(object sender, DocumentChangedEventArgs args)
        {
            using (QueryLock.CancelAndGetWriteLock())
            {
                _document = _documentContainer.Document;
                IList<IDocumentChangeListener> listeners;
                lock (_documentChangedEventHandlers)
                {
                    listeners = _documentChangedEventHandlers.ToArray();
                }
                foreach (var listener in listeners)
                {
                    listener.DocumentOnChanged(sender, args);
                }
            }
        }

        public SkylineWindow SkylineWindow { get { return _documentContainer as SkylineWindow; } }

        private ReplicateSummaries _replicateSummaries;
        public ReplicateSummaries GetReplicateSummaries()
        {
            ReplicateSummaries replicateSummaries;
            if (null == _replicateSummaries)
            {
                replicateSummaries = new ReplicateSummaries(Document);
            }
            else
            {
                replicateSummaries = _replicateSummaries.GetReplicateSummaries(Document);
            }
            return _replicateSummaries = replicateSummaries;
        }

        public ChromDataCache ChromDataCache { get; private set; }

        public override PropertyDescriptor GetPropertyDescriptor(Type type, string name)
        {
            var propertyDescriptor = base.GetPropertyDescriptor(type, name);
            if (null != propertyDescriptor)
            {
                return propertyDescriptor;
            }
            if (null == type)
            {
                return null;
            }
            propertyDescriptor = RatioPropertyDescriptor.GetProperty(Document, type, name);
            if (null != propertyDescriptor)
            {
                return propertyDescriptor;
            }
            if (name.StartsWith(AnnotationDef.ANNOTATION_PREFIX))
            {
                var annotationTargets = GetAnnotationTargets(type);
                if (!annotationTargets.IsEmpty)
                {
                    var annotationDef = new AnnotationDef(name.Substring(AnnotationDef.ANNOTATION_PREFIX.Length),
                        annotationTargets, AnnotationDef.AnnotationType.text, new string[0]);
                    return new AnnotationPropertyDescriptor(annotationDef, false);
                }
            }

            return null;
        }

        public override string GetColumnDescription(ColumnDescriptor columnDescriptor)
        {
            String description = base.GetColumnDescription(columnDescriptor);
            if (!string.IsNullOrEmpty(description))
            {
                return description;
            }
            ColumnCaption columnCaption = GetColumnCaption(columnDescriptor);
            if (columnCaption.IsLocalizable)
            {
                return ColumnToolTips.ResourceManager.GetString(columnCaption.InvariantCaption);
            }
            return null;
        }

        public ImmutableSortedList<ResultKey, Replicate> ReplicateList { get { return _replicates.Value; } }
        public IDictionary<ResultFileKey, ResultFile> ResultFileList { get { return _resultFiles.Value; } }

        public static DataSchemaLocalizer GetLocalizedSchemaLocalizer()
        {
            return new DataSchemaLocalizer(CultureInfo.CurrentCulture, ColumnCaptions.ResourceManager);
        }

        public void BeginBatchModifyDocument()
        {
            if (null != _batchChangesOriginalDocument)
            {
                throw new InvalidOperationException();
            }
            if (!ReferenceEquals(_document, _documentContainer.Document))
            {
                DocumentChangedEventHandler(_documentContainer, new DocumentChangedEventArgs(_document));
            }
            _batchChangesOriginalDocument = _document;
        }

        public void CommitBatchModifyDocument(string description)
        {
            if (null == _batchChangesOriginalDocument)
            {
                throw new InvalidOperationException();
            }
            string message = Resources.DataGridViewPasteHandler_EndDeferSettingsChangesOnDocument_Updating_settings;
            SkylineWindow.ModifyDocument(description, document =>
            {
                VerifyDocumentCurrent(_batchChangesOriginalDocument, document);
                using (var longWaitDlg = new LongWaitDlg
                {
                    Message = message
                })
                {
                    SrmDocument newDocument = null;
                    longWaitDlg.PerformWork(SkylineWindow, 1000, progressMonitor =>
                    {
                        var srmSettingsChangeMonitor = new SrmSettingsChangeMonitor(progressMonitor,
                            message);
                        newDocument = _document.EndDeferSettingsChanges(_batchChangesOriginalDocument.Settings, srmSettingsChangeMonitor);
                    });
                    return newDocument;
                }
            });
            _batchChangesOriginalDocument = null;
            DocumentChangedEventHandler(_documentContainer, new DocumentChangedEventArgs(_document));
        }

        public void RollbackBatchModifyDocument()
        {
            _batchChangesOriginalDocument = null;
            _document = _documentContainer.Document;
        }

        public void ModifyDocument(EditDescription editDescription, Func<SrmDocument, SrmDocument> action)
        {
            if (_batchChangesOriginalDocument == null)
            {
                SkylineWindow.ModifyDocument(editDescription.GetUndoText(DataSchemaLocalizer), action);
                return;
            }
            VerifyDocumentCurrent(_batchChangesOriginalDocument, _documentContainer.Document);
            _document = action(_document.BeginDeferSettingsChanges());
        }

        private void VerifyDocumentCurrent(SrmDocument expectedCurrentDocument, SrmDocument actualCurrentDocument)
        {
            if (!ReferenceEquals(expectedCurrentDocument, actualCurrentDocument))
            {
                throw new InvalidOperationException(Resources.SkylineDataSchema_VerifyDocumentCurrent_The_document_was_modified_in_the_middle_of_the_operation_);
            }
        }

        private ImmutableSortedList<ResultKey, Replicate> CreateReplicateList()
        {
            var srmDocument = Document;
            if (!srmDocument.Settings.HasResults)
            {
                return ImmutableSortedList<ResultKey, Replicate>.EMPTY;
            }
            return ImmutableSortedList<ResultKey, Replicate>.FromValues(
                Enumerable.Range(0, srmDocument.Settings.MeasuredResults.Chromatograms.Count)
                    .Select(replicateIndex =>
                    {
                        var replicate = new Replicate(this, replicateIndex);
                        return new KeyValuePair<ResultKey, Replicate>(new ResultKey(replicate, 0), replicate);
                    }), Comparer<ResultKey>.Default);
        }
 
        private IDictionary<ResultFileKey, ResultFile> CreateResultFileList()
        {
            return ReplicateList.Values.SelectMany(
                    replicate =>
                        replicate.ChromatogramSet.MSDataFileInfos.Select(
                            chromFileInfo => new ResultFile(replicate, chromFileInfo.FileId, 0)))
                .ToDictionary(resultFile => new ResultFileKey(resultFile.Replicate.ReplicateIndex,
                    resultFile.ChromFileInfoId, resultFile.OptimizationStep));
        }
    }
}
