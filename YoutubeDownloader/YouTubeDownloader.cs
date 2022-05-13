using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Converter;
using YoutubeExplode.Videos.Streams;

namespace YoutubeDownloader
{
    public partial class YouTubeDownloader : Form
    {
        private readonly YoutubeClient _youtubeClient;
        private StreamManifest _manifest;
        private Video _data;

        private readonly string _basePath = AppDomain.CurrentDomain.BaseDirectory;
        private readonly string _thumbNailInput = "input.webp";
        private readonly string _thumbNailOutput = "output.png";
        private readonly string _fileName = $"video{Guid.NewGuid()}.mp4";

        public YouTubeDownloader()
        {
            _youtubeClient = new YoutubeClient();

            InitializeComponent();

            if (File.Exists(_basePath + _thumbNailInput))
            {
                File.Delete(_basePath + _thumbNailInput);
            }

            if (File.Exists(_basePath + _thumbNailOutput))
            {
                File.Delete(_basePath + _thumbNailOutput);
            }
        }

        private async void SearchBtn_Click(object sender, EventArgs e)
        {
            _data = await GetVideoData(UrlTB.Text.Trim());

            if (_data != null)
            {
                ThumbnailPB.Visible = true;
                DownloadBtn.Visible = true;
                QualityCB.Visible = true;

                AuthorLabel.Text = _data.Author;
                DurationLabel.Text = _data.Duration;
                TitleLabel.Text = _data.Title;

                string ext = _data.Thumbnail.Split('.').LastOrDefault();

                if (ext == "webp")
                {
                    using (var wc = new WebClient())
                    {
                        wc.DownloadFile(_data.Thumbnail, _thumbNailInput);
                    }

                    await Task.Run(() =>
                    {
                        var converter = new GroupDocs.Conversion.Converter(_basePath + _thumbNailInput);
                        var convertOptions = converter.GetPossibleConversions()["png"].ConvertOptions;
                        converter.Convert(_thumbNailOutput, convertOptions);
                    });

                    ThumbnailPB.Image = Image.FromFile(_basePath + _thumbNailOutput);
                }
                else
                {
                    ThumbnailPB.Load(_data.Thumbnail);
                }
            }
            else
            {
                MessageBox.Show("Something went wrong! Check your Internet connection");
            }
        }

        private async void DownloadBtn_Click(object sender, EventArgs e)
        {
            await Download();
        }

        private async Task<Video> GetVideoData(string url)
        {
            try
            {
                var video = await _youtubeClient.Videos.GetAsync(url);
                _manifest = await _youtubeClient.Videos.Streams.GetManifestAsync(url);

                QualityCB.Items.AddRange(_manifest.GetVideoStreams().Select(s => s.VideoQuality.Label).Distinct().ToArray());
                QualityCB.SelectedItem = QualityCB.Items[0];

                return new Video
                {
                    Title = video.Title,
                    Author = video.Author.ChannelTitle,
                    Duration = video.Duration.ToString(),
                    Url = video.Url,
                    Thumbnail = video.Thumbnails.GetWithHighestResolution().Url
                };
                
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error occured: {ex.Message}");
            }

            return null;
        }

        private async Task Download()
        {
            try
            {
                string folderPath = null;

                using (var fbd = new FolderBrowserDialog())
                {
                    var result = fbd.ShowDialog();

                    if (result == DialogResult.OK && !String.IsNullOrWhiteSpace(fbd.SelectedPath))
                    {
                        folderPath = fbd.SelectedPath;
                    }
                }

                string path = $@"{folderPath}\{_fileName}" ?? _fileName;

                var audioStreamInfo = _manifest.GetAudioStreams().GetWithHighestBitrate();
                var videoStreamInfo = _manifest.GetVideoStreams().First(s => s.VideoQuality.Label == QualityCB.SelectedItem.ToString());
                var streamInfos = new IStreamInfo[] { audioStreamInfo, videoStreamInfo };

                await _youtubeClient.Videos.DownloadAsync(
                    streamInfos,
                    new ConversionRequestBuilder(path).Build(),
                    new Progress<double>(p => 
                    {
                        ProgressBar.Value = (int)(p * 100);
                        ProgressLabel.Text = (int)(p * 100) + "%";
                    })
                );
                
                MessageBox.Show("Download complete");
                Reset();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error occured: {ex.Message}");
            }
        }

        private void Reset()
        {
            ThumbnailPB.Visible = DownloadBtn.Visible = QualityCB.Visible = false;

            AuthorLabel.Text = DurationLabel.Text = TitleLabel.Text = UrlTB.Text = ProgressLabel.Text = String.Empty;

            ProgressBar.Value = 0;
        }
    }
}
