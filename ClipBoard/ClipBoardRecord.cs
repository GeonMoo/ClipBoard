namespace ClipBoard
{
    class ClipBoardRecord
    {
        private string _content;
        private string _resource;
        private long _timesCoppiedCount;
        private long _timesPastedCount;

        public ClipBoardRecord() { }
        
        public ClipBoardRecord(string content, string resource, long timesCoppiedCount, long timesPastedCount)
        {
            _content = content;
            _resource = resource;
            _timesCoppiedCount = timesCoppiedCount;
            _timesPastedCount = timesPastedCount;
        }

        public string Content
        {
            get { return _content; }
            set { _content = value; }
        }

        public string Resource
        {
            get { return _resource; }
            set { _resource = value; }
        }

        public long CoppiedCount
        {
            get { return _timesCoppiedCount; }
            set { _timesCoppiedCount = value; }
        }

        public long PastedCount
        {
            get { return _timesPastedCount; }
            set { _timesPastedCount = value; }
        }
    }
}
    