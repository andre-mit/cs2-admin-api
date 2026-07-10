namespace Cs2Admin.API.Services;

public class BaseUpdateState
{
    private readonly object _lock = new();

    private bool _isUpdating;
    public bool IsUpdating
    {
        get
        {
            lock (_lock) return _isUpdating;
        }
        set
        {
            lock (_lock) _isUpdating = value;
        }
    }

    private double _progressPercentage;
    public double ProgressPercentage
    {
        get
        {
            lock (_lock) return _progressPercentage;
        }
        set
        {
            lock (_lock) _progressPercentage = value;
        }
    }

    private string _downloadedBytes = "0";
    public string DownloadedBytes
    {
        get
        {
            lock (_lock) return _downloadedBytes;
        }
        set
        {
            lock (_lock) _downloadedBytes = value;
        }
    }

    private string _totalBytes = "0";
    public string TotalBytes
    {
        get
        {
            lock (_lock) return _totalBytes;
        }
        set
        {
            lock (_lock) _totalBytes = value;
        }
    }

    private string _status = "Idle";
    public string Status
    {
        get
        {
            lock (_lock) return _status;
        }
        set
        {
            lock (_lock) _status = value;
        }
    }
    
    public void Reset()
    {
        lock (_lock)
        {
            _isUpdating = false;
            _progressPercentage = 0;
            _downloadedBytes = "0";
            _totalBytes = "0";
            _status = "Idle";
        }
    }
}
