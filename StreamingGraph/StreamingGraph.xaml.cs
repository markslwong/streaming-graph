using System;
using System.Collections.Generic;;
using System.Diagnostics;


namespace StreamingGraph
{
    public partial class StreamingGraph
    {
        private readonly List<DataPoint> _points = new List<DataPoint>();

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

        private void Render()
        {
            Graph.Children.Clear();

            
        }
    }
}
