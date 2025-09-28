namespace Translator.Service
{
    public class AudioStream : Stream
    {
        private const int SAMPLES_PER_SECOND = 24000;
        private const int BYTES_PER_SAMPLE = 2;
        private const int CHANNELS = 1;

        // For simplicity, this is configured to use a static 10-second ring buffer.
        private readonly byte[] _buffer = new byte[BYTES_PER_SAMPLE * SAMPLES_PER_SECOND * CHANNELS * 10];
        private readonly object _bufferLock = new();

        private int _bufferReadPos = 0;
        private int _bufferWritePos = 0;

        private readonly ILogger<AudioStream> _logger;

        public volatile bool IsRecording = false;

        //public event Action? RecordingStarted;
        //public event Action? RecordingStopped;
        public event Action<byte[], int, int>? OnDataAvailable;

        public AudioStream(ILogger<AudioStream> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 这是个单例模式，如果启动这个方法全局都会开始录音
        /// </summary>
        public void StartRecording()
        {
            IsRecording = true;
            _logger.LogWarning("开始获取录音");
        }

        /// <summary>
        /// 这是个单例模式，如果停止这个方法全局都会停止录音
        /// </summary>
        public void StopRecording()
        {
            IsRecording = false;
            lock (_bufferLock)
            {
                _logger.LogDebug("清空缓冲区");
                _bufferReadPos = 0;
                _bufferWritePos = 0;
                Array.Clear(_buffer, 0, _buffer.Length);
            }
            _logger.LogWarning("停止获取录音");
        }

        protected override void Dispose(bool disposing)
        {
            StopRecording();
            OnDataAvailable = null;
            _logger.LogWarning("SocketAudioStream 已释放");
            base.Dispose(disposing);
        }


        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int totalCount = count;

            int GetBytesAvailable() => _bufferWritePos < _bufferReadPos
                ? _bufferWritePos + (_buffer.Length - _bufferReadPos)
                : _bufferWritePos - _bufferReadPos;

            // For simplicity, we'll block until all requested data is available and not perform partial reads.
            while (GetBytesAvailable() < count || !IsRecording)
            {
                await Task.Delay(100, cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return 0;
            }

            lock (_bufferLock)
            {
                if (_bufferReadPos + count >= _buffer.Length && IsRecording)
                {
                    int bytesBeforeWrap = _buffer.Length - _bufferReadPos;
                    Array.Copy(
                        sourceArray: _buffer,
                        sourceIndex: _bufferReadPos,
                        destinationArray: buffer,
                        destinationIndex: offset,
                        length: bytesBeforeWrap);
                    _bufferReadPos = 0;
                    count -= bytesBeforeWrap;
                    offset += bytesBeforeWrap;
                }

                Array.Copy(_buffer, _bufferReadPos, buffer, offset, count);
                _bufferReadPos += count;
            }

            return totalCount;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <summary>
        /// 这里相当于 WaveInEvent 触发了 DataAvailable 事件。
        /// </summary>
        /// <param name="buffer">这个参数相当于waveInEvent.DataAvailable 中e.Buffer</param>
        /// <param name="offset">这里传递结果会是0</param>
        /// <param name="count">这个参数相当于waveInEvent.DataAvailable 中e.BytesRecorded</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override void Write(byte[] buffer, int offset, int count)
        {
            // 注意：写入不再依赖 IsRecording，这样 UDP 持续写入不会因 StopRecording 被丢弃。
            lock (_bufferLock)
            {
                int bytesToCopy = count;
                if (_bufferWritePos + bytesToCopy >= _buffer.Length)
                {
                    int bytesToCopyBeforeWrap = _buffer.Length - _bufferWritePos;
                    Array.Copy(buffer, 0, _buffer, _bufferWritePos, bytesToCopyBeforeWrap);
                    bytesToCopy -= bytesToCopyBeforeWrap;
                    _bufferWritePos = 0;
                }
                Array.Copy(buffer, count - bytesToCopy, _buffer, _bufferWritePos, bytesToCopy);
                _bufferWritePos += bytesToCopy;

                OnDataAvailable?.Invoke(buffer, offset, count);
            }
        }
    }
}
