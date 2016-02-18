#region S# License
/******************************************************************************************
NOTICE!!!  This program and source code is owned and licensed by
StockSharp, LLC, www.stocksharp.com
Viewing or use of this code requires your acceptance of the license
agreement found at https://github.com/StockSharp/StockSharp/blob/master/LICENSE
Removal of this comment is a violation of the license agreement.

Project: StockSharp.Studio.Controls.ControlsPublic
File: CandleChartPanel.xaml.cs
Created: 2015, 11, 11, 2:32 PM

Copyright 2010 by StockSharp, LLC
*******************************************************************************************/
#endregion S# License

namespace StockSharp.Studio.Controls
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Ecng.Configuration;
	using Ecng.Serialization;
	using Ecng.ComponentModel;
	using Ecng.Xaml.Charting.Common;
	using Ecng.Common;

	using StockSharp.BusinessEntities;
	using StockSharp.Algo.Candles;
	using StockSharp.Configuration;
	using StockSharp.Localization;
	using StockSharp.Studio.Core.Commands;
	using StockSharp.Xaml.Charting;

	[DisplayNameLoc(LocalizedStrings.Str3200Key)]
	[DescriptionLoc(LocalizedStrings.Str3201Key)]
	[Icon("images/chart_24x24.png")]
	public partial class CandleChartPanel
	{
		readonly object _lock = new object();
		CandleSeries _series;

		public CandleSeries Series
		{
			get { return _series; }
			private set { SetField(ref _series, value, nameof(Series)); }
		}

		ChartArea _area = new ChartArea();
		ChartCandleElement _candleElement;
		readonly TimeSpan _timeFrame = TimeSpan.FromMinutes(5);

		public CandleChartPanel()
		{
			InitializeComponent();

			ChartPanel.SettingsChanged += OnSettingsUpdated;

			var cmdSvc = ConfigManager.GetService<IStudioCommandService>();
			cmdSvc.Register<CandleDataCommand>(this, false, OnCandleCommand);
			cmdSvc.Register<CandleChartResetCommand>(this, true, cmd => Reset());
			cmdSvc.Register<SelectCommand>(this, true, cmd =>
			{
				if(ChartPanel.Security != null)
					return;

				var sec = cmd.Instance as Security;
				if(sec == null)
					return;

				ChartPanel.Security = sec;
			});
			
			ChartPanel.RegisterOrder += order => new RegisterOrderCommand(order).Process(this);
			ChartPanel.MinimumRange = 200;
			ChartPanel.IsInteracted = true;
			ChartPanel.FillIndicators();

			WhenLoaded(() =>
			{
				OnSettingsUpdated();
			});
		}

		private void OnCandleCommand(CandleDataCommand cmd)
		{
			lock (_lock)
			{
				if(cmd.Series != Series || Series == null)
					return;

				var values = cmd.Candles.Select(c => new RefPair<DateTimeOffset, IDictionary<IChartElement, object>>
														(c.OpenTime, new Dictionary<IChartElement, object> {{ _candleElement, c }}));

				ChartPanel.Draw(values);
			}
		}

		public override void Dispose()
		{
			var cmdSvc = ConfigManager.GetService<IStudioCommandService>();

			Reset();

			cmdSvc.UnRegister<CandleDataCommand>(this);
		}

		public override void Load(SettingsStorage storage)
		{
			Reset();

			base.Load(storage);

			lock (_lock)
			{
				Series = new CandleSeries();
				var ser = storage.GetValue<SettingsStorage>(nameof(Series));
				if(ser != null)
					Series.Load(ser);

				var panel = storage.GetValue<SettingsStorage>(nameof(ChartPanel));
				if(panel != null)
					ChartPanel.Load(panel);

				OnSettingsUpdated();
			}
		}

		public override void Save(SettingsStorage storage)
		{
			base.Save(storage);

			if(Series != null)
				storage.SetValue(nameof(Series), Series.Save());
			storage.SetValue(nameof(ChartPanel), ChartPanel.Save());
		}

		private void Reset()
		{
			lock (_lock)
			{
				_candleElement.Do(e =>
				{
					ChartPanel.RemoveElement(_area, e);
					_candleElement = null;
				});

				ChartPanel.ClearAreas();
				_area = null;

				Series.Do(s =>
				{
					if(Series.Security != null)
						new UnsubscribeCandleChartCommand(s).Process(this);
					Series = null;
				});
			}
		}

		private void OnSettingsUpdated()
		{
			if(ChartPanel.Security == Series.With(s => s.Security))
				return;

			lock (_lock)
			{
				Reset();

				if(ChartPanel.Security == null)
					return;

				Series = new CandleSeries(typeof(TimeFrameCandle), ChartPanel.Security, _timeFrame)
				{
					From = DateTimeOffset.Now - TimeSpan.FromDays(7)
				};

				_area = new ChartArea();
				ChartPanel.AddArea(_area);

				_candleElement = new ChartCandleElement();
				ChartPanel.AddElement(_area, _candleElement);
			}

			new SubscribeCandleChartCommand(Series).Process(this);
		}

	}
}