using Playnite.SDK;
using Playnite.SDK.Data;
using ScreenshotsVisualizer.Models;
using System;
using System.Collections.Generic;

namespace ScreenshotsVisualizer.ViewModels.Settings
{
    /// <summary>
    /// View model wrapper around <see cref="SsvImageConversionCustomCmd"/> for list and editor bindings.
    /// </summary>
    public class SsvImageConversionCustomCmdItem : ObservableObject
    {
        private readonly SsvImageConversionCustomCmd _model;

        /// <summary>
        /// Initializes a new conversion command entry with default values.
        /// </summary>
        public SsvImageConversionCustomCmdItem()
            : this(new SsvImageConversionCustomCmd())
        {
        }

        /// <summary>
        /// Initializes a new conversion command entry wrapping an existing model.
        /// </summary>
        /// <param name="model">Persisted conversion profile.</param>
        public SsvImageConversionCustomCmdItem(SsvImageConversionCustomCmd model)
        {
            _model = model ?? new SsvImageConversionCustomCmd();
            if (_model.Id == Guid.Empty)
            {
                _model.Id = Guid.NewGuid();
            }
        }

        /// <summary>
        /// Gets the stable profile identifier.
        /// </summary>
        public Guid Id => _model.Id;

        /// <summary>
        /// Gets or sets the display name shown in menus and settings.
        /// </summary>
        public string Name
        {
            get => _model.Name;
            set
            {
                if (_model.Name != value)
                {
                    _model.Name = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the target output format extension without a leading dot.
        /// </summary>
        public string OutputFormat
        {
            get => _model.OutputFormat;
            set
            {
                if (_model.OutputFormat != value)
                {
                    _model.OutputFormat = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the quality slider value exposed to the UI.
        /// </summary>
        public int QualityValue
        {
            get => _model.Quality ?? 98;
            set
            {
                int clamped = value;
                if (clamped < 1)
                {
                    clamped = 1;
                }

                if (clamped > 100)
                {
                    clamped = 100;
                }

                if (_model.Quality != clamped)
                {
                    _model.Quality = clamped;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether metadata is stripped during conversion.
        /// </summary>
        public bool StripMetadata
        {
            get => _model.StripMetadata;
            set
            {
                if (_model.StripMetadata != value)
                {
                    _model.StripMetadata = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets additional ImageMagick arguments.
        /// </summary>
        public string ExtraArguments
        {
            get => _model.ExtraArguments;
            set
            {
                if (_model.ExtraArguments != value)
                {
                    _model.ExtraArguments = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets a display summary for list rows.
        /// </summary>
        public string DisplaySummary
        {
            get
            {
                string format = string.IsNullOrWhiteSpace(OutputFormat) ? "jpg" : OutputFormat.Trim();
                return string.Format("{0} ({1})", Name, format);
            }
        }

        /// <summary>
        /// Creates a deep copy of the wrapped model for editor working copies.
        /// </summary>
        /// <returns>A new item with cloned settings.</returns>
        public SsvImageConversionCustomCmdItem Clone()
        {
            return new SsvImageConversionCustomCmdItem(Serialization.GetClone(_model));
        }

        /// <summary>
        /// Creates a shallow copy of the wrapped model for persistence.
        /// </summary>
        /// <returns>A new model instance with the same values.</returns>
        public SsvImageConversionCustomCmd ToModel()
        {
            return Serialization.GetClone(_model);
        }

        /// <summary>
        /// Copies all values from another entry into this one.
        /// </summary>
        /// <param name="source">Source entry to copy from.</param>
        public void ApplyFrom(SsvImageConversionCustomCmdItem source)
        {
            if (source == null)
            {
                return;
            }

            ApplyFrom(source.ToModel());
        }

        /// <summary>
        /// Copies model values into this entry.
        /// </summary>
        /// <param name="model">Model to apply.</param>
        public void ApplyFrom(SsvImageConversionCustomCmd model)
        {
            if (model == null)
            {
                return;
            }

            _model.Id = model.Id == Guid.Empty ? Guid.NewGuid() : model.Id;
            Name = model.Name;
            OutputFormat = model.OutputFormat;
            QualityValue = model.Quality ?? 98;
            StripMetadata = model.StripMetadata;
            ExtraArguments = model.ExtraArguments;
            _model.DeleteOriginal = true;
        }
    }
}
