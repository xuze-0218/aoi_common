using Cognex.VisionPro;
using Cognex.VisionPro.ImageFile;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;


namespace aoi_common.Services
{
    public interface IImageSource
    {
        bool HasNext();

        ICogImage GetNext();

        string GetCurrentImageName();

        int TotalCount { get; }

        int CurrentIndex { get; }

        void Reset();

        void Dispose();
    }

    /// <summary>
    /// 离线文件
    /// </summary>
    public class LocalFileImageSource : IImageSource
    {
        private readonly string _filePath;
        private CogImageFileTool _imageFileTool;
        private readonly ILogger _logger;
        private bool _isDisposed = false;

        public int TotalCount
        {
            get { return 1; }
        }

        public int CurrentIndex
        {
            get { return _isDisposed ? 0 : 1; }
        }

        public LocalFileImageSource(string filePath, ILogger logger)
        {
            _filePath = filePath;
            _logger = logger;
            _imageFileTool = new CogImageFileTool();
        }

        public bool HasNext()
        {
            if (_isDisposed) return false;
            return !_isDisposed;
        }

        public ICogImage GetNext()
        {
            if (_isDisposed)
                throw new InvalidOperationException("图像源已释放");

            if (!File.Exists(_filePath))
                throw new FileNotFoundException("文件不存在: " + _filePath);

            try
            {
                _imageFileTool.Operator.Open(_filePath, CogImageFileModeConstants.Read);
                _imageFileTool.Run();
                ICogImage image = _imageFileTool.OutputImage;
                _isDisposed = true; // 单文件模式只返回一次
                return image;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "读取图像文件失败");
                throw;
            }
        }

        public string GetCurrentImageName()
        {
            return Path.GetFileName(_filePath);
        }

        public void Reset()
        {
            _isDisposed = false;
        }

        public void Dispose()
        {
            if (_imageFileTool != null)
            {
                try
                {
                    _imageFileTool.Operator.Close();
                }
                catch { }
                _imageFileTool.Dispose();
                _imageFileTool = null;
            }
            _isDisposed = true;
        }
    }

    /// <summary>
    /// 离线文件夹
    /// </summary>
    public class LocalFolderImageSource : IImageSource
    {
        private readonly string _folderPath;
        private readonly string[] _supportedExtensions = { ".bmp", ".jpg", ".jpeg", ".png", ".tiff", ".tif" };
        private List<string> _imageFiles;
        private int _currentIndex = -1;
        private CogImageFileTool _imageFileTool;
        private readonly ILogger _logger;

        public int TotalCount
        {
            get { return _imageFiles != null ? _imageFiles.Count : 0; }
        }

        public int CurrentIndex
        {
            get { return _currentIndex; }
        }

        public LocalFolderImageSource(string folderPath, ILogger logger)
        {
            _folderPath = folderPath;
            _logger = logger;
            _imageFileTool = new CogImageFileTool();
            InitializeImageList();
        }

        private void InitializeImageList()
        {
            try
            {
                if (!Directory.Exists(_folderPath))
                    throw new DirectoryNotFoundException("文件夹不存在: " + _folderPath);

                _imageFiles = new List<string>();
                DirectoryInfo dirInfo = new DirectoryInfo(_folderPath);
                FileInfo[] files = dirInfo.GetFiles();

                foreach (FileInfo file in files)
                {
                    if (Array.Exists(_supportedExtensions, ext => ext.Equals(file.Extension, StringComparison.OrdinalIgnoreCase)))
                    {
                        _imageFiles.Add(file.FullName);
                    }
                }

                _imageFiles.Sort(); // 按文件名排序
                _logger.Debug("文件夹图像源初始化完成，共找到 {Count} 张图像", _imageFiles.Count);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "初始化文件夹图像列表失败");
                throw;
            }
        }

        public bool HasNext()
        {
            return _currentIndex + 1 < _imageFiles.Count;
        }

        public ICogImage GetNext()
        {
            if (!HasNext())
                throw new InvalidOperationException("没有更多图像了");

            _currentIndex++;
            string filePath = _imageFiles[_currentIndex];

            try
            {
                _imageFileTool.Operator.Close();
                _imageFileTool.Operator.Open(filePath, CogImageFileModeConstants.Read);
                _imageFileTool.Run();
                ICogImage image = _imageFileTool.OutputImage;
                _logger.Debug("加载图像 [{Current}/{Total}]: {FileName}", _currentIndex + 1, _imageFiles.Count, Path.GetFileName(filePath));
                return image;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "读取图像文件失败: {FilePath}", filePath);
                throw;
            }
        }

        public string GetCurrentImageName()
        {
            if (_currentIndex < 0 || _currentIndex >= _imageFiles.Count)
                return string.Empty;

            return Path.GetFileName(_imageFiles[_currentIndex]);
        }

        public void Reset()
        {
            _currentIndex = -1;
        }

        public void Dispose()
        {
            if (_imageFileTool != null)
            {
                try
                {
                    _imageFileTool.Operator.Close();
                }
                catch { }
                _imageFileTool.Dispose();
                _imageFileTool = null;
            }

            if (_imageFiles != null)
            {
                _imageFiles.Clear();
                _imageFiles = null;
            }
        }
    }


}
