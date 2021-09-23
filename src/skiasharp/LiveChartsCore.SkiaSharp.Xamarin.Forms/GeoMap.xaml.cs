﻿// The MIT License(MIT)
//
// Copyright(c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using LiveChartsCore.Drawing;
using LiveChartsCore.Geo;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView.Drawing;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace LiveChartsCore.SkiaSharpView.Xamarin.Forms
{
    /// <summary>
    /// Defines a geographic map.
    /// </summary>
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class GeoMap : ContentView, IGeoMapView<SkiaSharpDrawingContext>
    {
        private readonly CollectionDeepObserver<IMapElement> _shapesObserver;
        private readonly GeoMap<SkiaSharpDrawingContext> _core;

        /// <summary>
        /// Initializes a new instance of the <see cref="GeoMap"/> class.
        /// </summary>
        public GeoMap()
        {
            InitializeComponent();
            if (!LiveCharts.IsConfigured) LiveCharts.Configure(LiveChartsSkiaSharp.DefaultPlatformBuilder);
            _core = new GeoMap<SkiaSharpDrawingContext>(this);
            _shapesObserver = new CollectionDeepObserver<IMapElement>(
                (object? sender, NotifyCollectionChangedEventArgs e) => _core?.Update(),
                (object? sender, PropertyChangedEventArgs e) => _core?.Update(),
                true);
            SetValue(ShapesProperty, Enumerable.Empty<IMapElement>());
            SetValue(ActiveMapProperty, Maps.GetWorldMap());
            SetValue(SyncContextProperty, new object());
            SizeChanged += GeoMap_SizeChanged;
        }

        #region dependency props

        /// <summary>
        /// The active map property
        /// </summary>
        public static readonly BindableProperty ActiveMapProperty =
            BindableProperty.Create(
                nameof(ActiveMap), typeof(GeoJsonFile), typeof(GeoMap), null, BindingMode.Default, null, OnBindablePropertyChanged);

        /// <summary>
        /// The sync context property
        /// </summary>
        public static readonly BindableProperty SyncContextProperty =
            BindableProperty.Create(
                nameof(SyncContext), typeof(object), typeof(GeoMap), null, BindingMode.Default, null, OnBindablePropertyChanged);

        /// <summary>
        /// The map projection property
        /// </summary>
        public static readonly BindableProperty MapProjectionProperty =
            BindableProperty.Create(
                nameof(MapProjection), typeof(MapProjection), typeof(GeoMap),
                MapProjection.Default, BindingMode.Default, null, OnBindablePropertyChanged);

        /// <summary>
        /// The heat map property
        /// </summary>
        public static readonly BindableProperty HeatMapProperty =
            BindableProperty.Create(
                nameof(HeatMap), typeof(LvcColor[]), typeof(GeoMap),
                new[]
                {
                  LvcColor.FromArgb(255, 179, 229, 252), // cold (min value)
                  LvcColor.FromArgb(255, 2, 136, 209) // hot (max value)
                }, BindingMode.Default, null, OnBindablePropertyChanged);

        /// <summary>
        /// The color stops property
        /// </summary>
        public static readonly BindableProperty ColorStopsProperty =
            BindableProperty.Create(
                nameof(ColorStops), typeof(double[]), typeof(GeoMap), null, BindingMode.Default, null);

        /// <summary>
        /// The stroke property
        /// </summary>
        public static readonly BindableProperty StrokeProperty =
            BindableProperty.Create(
                nameof(Stroke), typeof(IPaint<SkiaSharpDrawingContext>), typeof(GeoMap),
                  new SolidColorPaint(new SKColor(224, 224, 224, 255)) { IsStroke = true },
                  BindingMode.Default, null, OnBindablePropertyChanged);


        /// <summary>
        /// The fill property
        /// </summary>
        public static readonly BindableProperty FillProperty =
           BindableProperty.Create(
               nameof(Fill), typeof(IPaint<SkiaSharpDrawingContext>), typeof(GeoMap),
                new SolidColorPaint(new SKColor(250, 250, 250, 255)) { IsFill = true },
                BindingMode.Default, null, OnBindablePropertyChanged);

        /// <summary>
        /// The values property
        /// </summary>
        public static readonly BindableProperty ShapesProperty =
           BindableProperty.Create(
               nameof(Shapes), typeof(IEnumerable<IMapElement>), typeof(GeoMap), Enumerable.Empty<IMapElement>(),
               BindingMode.Default, null, (BindableObject o, object oldValue, object newValue) =>
               {
                   var chart = (GeoMap)o;
                   var seriesObserver = chart._shapesObserver;
                   seriesObserver.Dispose((IEnumerable<IMapElement>)oldValue);
                   seriesObserver.Initialize((IEnumerable<IMapElement>)newValue);
                   chart._core.Update();
               });

        #endregion

        #region props

        /// <inheritdoc cref="IGeoMapView{TDrawingContext}.AutoUpdateEnabled" />
        public bool AutoUpdateEnabled { get; set; } = true;

        /// <inheritdoc cref="IGeoMapView{TDrawingContext}.DesignerMode" />
        bool IGeoMapView<SkiaSharpDrawingContext>.DesignerMode => DesignMode.IsDesignModeEnabled;

        /// <inheritdoc cref="IGeoMapView{TDrawingContext}.SyncContext" />
        public object SyncContext
        {
            get => GetValue(SyncContextProperty);
            set => SetValue(SyncContextProperty, value);
        }

        /// <inheritdoc cref="IGeoMapView{TDrawingContext}.Canvas"/>
        public MotionCanvas<SkiaSharpDrawingContext> Canvas => canvas.CanvasCore;

        /// <inheritdoc cref="IGeoMapView{TDrawingContext}.ActiveMap"/>
        public GeoJsonFile ActiveMap
        {
            get => (GeoJsonFile)GetValue(ActiveMapProperty);
            set => SetValue(ActiveMapProperty, value);
        }

        /// <inheritdoc cref="IGeoMapView{TDrawingContext}.Width"/>
        float IGeoMapView<SkiaSharpDrawingContext>.Width => (float)(canvas.Width * DeviceDisplay.MainDisplayInfo.Density);

        /// <inheritdoc cref="IGeoMapView{TDrawingContext}.Height"/>
        float IGeoMapView<SkiaSharpDrawingContext>.Height => (float)(canvas.Height * DeviceDisplay.MainDisplayInfo.Density);

        /// <inheritdoc cref="IGeoMapView{TDrawingContext}.MapProjection"/>
        public MapProjection MapProjection
        {
            get => (MapProjection)GetValue(MapProjectionProperty);
            set => SetValue(MapProjectionProperty, value);
        }

        /// <inheritdoc cref="IGeoMapView{TDrawingContext}.HeatMap"/>
        public LvcColor[] HeatMap
        {
            get => (LvcColor[])GetValue(HeatMapProperty);
            set => SetValue(HeatMapProperty, value);
        }

        /// <inheritdoc cref="IGeoMapView{TDrawingContext}.ColorStops"/>
        public double[]? ColorStops
        {
            get => (double[])GetValue(ColorStopsProperty);
            set => SetValue(ColorStopsProperty, value);
        }

        /// <inheritdoc cref="IGeoMapView{TDrawingContext}.Stroke"/>
        public IPaint<SkiaSharpDrawingContext>? Stroke
        {
            get => (IPaint<SkiaSharpDrawingContext>)GetValue(StrokeProperty);
            set
            {
                if (value is not null) value.IsStroke = true;
                SetValue(StrokeProperty, value);
            }
        }

        /// <inheritdoc cref="IGeoMapView{TDrawingContext}.Fill"/>
        public IPaint<SkiaSharpDrawingContext>? Fill
        {
            get => (IPaint<SkiaSharpDrawingContext>)GetValue(FillProperty);
            set
            {
                if (value is not null) value.IsFill = true;
                SetValue(FillProperty, value);
            }
        }

        /// <inheritdoc cref="IGeoMapView{TDrawingContext}.Shapes"/>
        public IEnumerable<IMapElement> Shapes
        {
            get => (IEnumerable<IMapElement>)GetValue(ShapesProperty);
            set => SetValue(ShapesProperty, value);
        }

        #endregion

        void IGeoMapView<SkiaSharpDrawingContext>.InvokeOnUIThread(Action action)
        {
            MainThread.BeginInvokeOnMainThread(action);
        }

        private void GeoMap_SizeChanged(object sender, EventArgs e)
        {
            _core?.Update();
        }

        private static void OnBindablePropertyChanged(BindableObject o, object oldValue, object newValue)
        {
            var chart = (GeoMap)o;
            if (chart._core is null) return;
            chart._core.Update();
        }
    }
}
