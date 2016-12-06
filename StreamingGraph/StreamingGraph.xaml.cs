using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;


namespace StreamingGraph
{
    public partial class StreamingGraph
    {
        private readonly List<DataPoint> _points = new List<DataPoint>();
        private readonly DispatcherTimer _timer;

        private class DataPoint
        {
            public DateTime Time      { get; set; }
            public int      Frequency { get; set; }

            public void Swallow(DataPoint other)
            {
                var timeDifference = other.Time - Time;
                var totalFrequency = other.Frequency + Frequency;

                var weightedTimeDifference = timeDifference.Ticks * ((double)other.Frequency / totalFrequency);

                Time += TimeSpan.FromTicks((long)weightedTimeDifference);
                Frequency += other.Frequency;
            }
        }

        public StreamingGraph()
        {
            InitializeComponent();

            _timer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs args)
        {
            _timer.Tick += (s, e) => Render();
            _timer.Start();
        }
        
        private void OnUnloaded(object sender, RoutedEventArgs args)
        {
            _timer.Stop();
        }

        public void Add(DateTime time, int frequency)
        {
            Debug.Assert(time <= DateTime.Now, "Cannot add data that has been generated for a future date and time.");

            var newPoint = new DataPoint
            {
                Time      = time,
                Frequency = frequency
            };

            if (_points.Count == 0 || 
                _points[_points.Count - 1].Time <= time)
            {
                _points.Add(newPoint);
            }
            else
            {
                for (var i = _points.Count - 1; i >= 0; --i)
                {
                    var point = _points[i];

                    if (point.Time > time)
                    {
                        _points.Insert(i, newPoint);
                        break;
                    }
                }    
            }
        }

        public DateTime GraphStartTime { get { return GraphEndTime - GraphTimeSpan; } }
        public DateTime GraphEndTime { get; private set; }

        public TimeSpan GraphTimeSpanMin { get; set; }
        public TimeSpan GraphTimeSpanMax { get; set; }

        public TimeSpan GraphTimeSpan
        {
            get
            {
                if (_points.Count == 0)
                    return GraphTimeSpanMin;

                if (_points[0].Time < GraphEndTime - GraphTimeSpanMax + SegmentTimeSpan)
                    return GraphTimeSpanMax;

                return GraphEndTime - _points[0].Time;
            }
        }

        public int SegmentCount { get; set; }

        public int FrequencyLabelsCount { get; set; }

        private TimeSpan SegmentTimeSpan
        {
            get { return TimeSpan.FromTicks(GraphTimeSpan.Ticks / SegmentCount); }
        }

        private DateTime GetSegmentStart(int segmentIndex)
        {
            return GraphStartTime + TimeSpan.FromTicks(SegmentTimeSpan.Ticks * segmentIndex);
        }

        private DateTime GetSegmentEnd(int segmentIndex)
        {
            return GetSegmentStart(segmentIndex + 1);
        }

        private void Update()
        {
            GraphEndTime = DateTime.Now;

            // Remove all points earlier than our graph start time.
            while (_points.Count > 0)
            {
                if (_points[0].Time >= GraphStartTime)
                    break;
                
                _points.RemoveAt(0);
            }

            if (_points.Count == 0)
                return;

            var indexStart = 0;

            for (var segment = 0; segment < SegmentCount; ++segment)
            {
                var segmentStart = GetSegmentStart(segment);
                var segmentEnd = GetSegmentEnd(segment);

                if (indexStart >= _points.Count)
                    return;

                var pointCurrent = _points[indexStart];

                Debug.Assert(pointCurrent.Time >= segmentStart);

                if (pointCurrent.Time < segmentEnd)
                {
                    var indexNext = indexStart + 1;

                    while (indexNext < _points.Count)
                    {
                        var pointNext = _points[indexNext];

                        Debug.Assert(pointNext.Time >= segmentStart);

                        if (pointNext.Time >= segmentEnd)
                            break;

                        pointCurrent.Swallow(pointNext);

                        _points.RemoveAt(indexNext);
                    }

                    indexStart = indexNext;
                }
            }
        }

        private int GetSegmentFrequency(int segmentIndex)
        {
            var segmentStart = GetSegmentStart(segmentIndex);
            var segmentEnd   = GetSegmentEnd(segmentIndex);

            for (var i = 0; i < _points.Count; ++i)
            {
                var point = _points[i];

                if (point.Time >= segmentEnd)
                    return 0;

                if (point.Time >= segmentStart)
                    return point.Frequency;
            }
            return 0;
        }

        private void Render()
        {
            Update();

            if (SegmentCount == 0)
                return;

            Graph.Children.Clear();

            var bezier = new PolyBezierSegment();
            var maxFrequency = _points.Max(x => x.Frequency);
            var segementCenterOffset = 1.0 / SegmentCount * 0.5;

            var startFrequency = GetSegmentFrequency(0);

            bezier.Points.Add(new Point(0, (double)startFrequency / maxFrequency));

            for (var i = 0; i < SegmentCount; ++i)
            {
                var frequency = GetSegmentFrequency(i);

                var x = (double)i / SegmentCount + segementCenterOffset;
                var y = (double)frequency / maxFrequency;

                bezier.Points.Add(new Point(x, y));
            }

            var endFrequency = GetSegmentFrequency(SegmentCount - 1);

            bezier.Points.Add(new Point(1, (double)endFrequency / maxFrequency));
        }
    }
}
