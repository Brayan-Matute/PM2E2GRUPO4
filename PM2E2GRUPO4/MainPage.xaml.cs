using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using Newtonsoft.Json;
using Plugin.Media;
using Plugin.Media.Abstractions;
using Plugin.Maui.Audio;
using SkiaSharp;
using System.Globalization;
using System.Timers;

namespace PM2E2GRUPO4
{
    public partial class MainPage : ContentPage
    {
        readonly IAudioManager _audioManager;
        readonly IAudioRecorder _audioRecorder;
        string _fileName;
        private string _rutaImagen;
        private string _base64Image;
        private System.Timers.Timer _timer;
        private int _secondsElapsed;

        public MainPage(IAudioManager audioManager)
        {
            InitializeComponent();
            _audioManager = audioManager;
            _audioRecorder = audioManager.CreateRecorder();
            _rutaImagen = string.Empty;
            _base64Image = string.Empty;
            LoadLocationDataAsync();

            _timer = new System.Timers.Timer(1000);
            _timer.Elapsed += OnTimedEvent;
        }

        private async void OnStartRecordingClicked(object sender, EventArgs e)
        {
            if (await Permissions.RequestAsync<Permissions.Microphone>() != PermissionStatus.Granted)
            {
                await DisplayAlert("Permiso requerido", "Se necesita permiso para acceder al micrófono.", "OK");
                return;
            }
            if (string.IsNullOrWhiteSpace(FileNameEntry.Text) || string.IsNullOrWhiteSpace(DescriptionEntry.Text))
            {
                await DisplayAlert("Campos requeridos", "Por favor ingrese un nombre y una descripción para grabar el audio.", "OK");
                return;
            }
            _fileName = FileNameEntry.Text.Trim();

            if (!_audioRecorder.IsRecording)
            {
                await _audioRecorder.StartAsync();
                StartRecordingButton.IsVisible = false;
                StopRecordingButton.IsVisible = true;
                _secondsElapsed = 0;
                _timer.Start();
            }
        }

        private async void OnStopRecordingClicked(object sender, EventArgs e)
        {
            if (_audioRecorder.IsRecording)
            {
                await _audioRecorder.StopAsync();
                _timer.Stop();
                StopRecordingButton.IsVisible = false;
                SaveSiteButton.IsVisible = true;
                ResumeRecordingButton.IsVisible = true;
            }
        }

        private async void OnResumeRecordingClicked(object sender, EventArgs e)
        {
            if (!_audioRecorder.IsRecording)
            {
                await _audioRecorder.StartAsync();
                ResumeRecordingButton.IsVisible = false;
                StopRecordingButton.IsVisible = true;
                SaveSiteButton.IsVisible = true;
                _timer.Start();
            }
        }

        private async void OnSaveSiteClicked(object sender, EventArgs e)
        {
            try
            {
                if (!_audioRecorder.IsRecording)
                {
                    var recordedAudio = await _audioRecorder.StopAsync();
                    var audioStream = recordedAudio.GetAudioStream();

                    using (var memoryStream = new MemoryStream())
                    {
                        await audioStream.CopyToAsync(memoryStream);
                        byte[] audioBytes = memoryStream.ToArray();
                        string base64Audio = Convert.ToBase64String(audioBytes);

                        await SendAudioInfoToApi(base64Audio);
                    }

                    await DisplayAlert("Audio Guardado", "Audio guardado y enviado a la API.", "OK");
                    FileNameEntry.Text = string.Empty;
                    DescriptionEntry.Text = string.Empty;
                    StartRecordingButton.IsVisible = true;
                    StopRecordingButton.IsVisible = false;
                    ResumeRecordingButton.IsVisible = false;
                    _timer.Stop();
                    RecordingTimeLabel.Text = "00:00";
                    _secondsElapsed = 0;
                }
                else
                {
                    await DisplayAlert("Error", "No se puede guardar el audio mientras se está grabando.", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Excepción: {ex.Message}", "OK");
            }
        }

        private async Task SendAudioInfoToApi(string base64Audio)
        {
            try
            {
                string description = DescriptionEntry.Text.Trim();

                if (!double.TryParse(LongitudeEntry.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double longitude))
                {
                    await DisplayAlert("Error", "La longitud no es válida. Asegúrate de que sea un número.", "OK");
                    return;
                }

                if (!double.TryParse(LatitudeEntry.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double latitude))
                {
                    await DisplayAlert("Error", "La latitud no es válida. Asegúrate de que sea un número.", "OK");
                    return;
                }

                var audioInfo = new
                {
                    audioFile = base64Audio,
                    fecha = DateTime.Now.ToString("yyyy-MM-dd"),
                    descripcion = description,
                    longitud = longitude,
                    latitud = latitude,
                    fotografia = _base64Image
                };

                using (HttpClient client = new HttpClient())
                {
                    var json = JsonConvert.SerializeObject(audioInfo);
                    var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await client.PostAsync(Config.Config.EndPointCreate, content);

                    if (response.IsSuccessStatusCode)
                    {
                        await DisplayAlert("Éxito", "Información enviada a la API.", "OK");
                    }
                    else
                    {
                        await DisplayAlert("Error", "Error al enviar la información a la API.", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Error al enviar la información a la API: {ex.Message}", "OK");
            }
        }

        private void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            _secondsElapsed++;
            Device.BeginInvokeOnMainThread(() =>
            {
                RecordingTimeLabel.Text = TimeSpan.FromSeconds(_secondsElapsed).ToString(@"mm\:ss");
            });
        }

        private async void OnImageTapped(object sender, EventArgs e)
        {
            var cameraPermission = await Permissions.RequestAsync<Permissions.Camera>();

            if (cameraPermission != PermissionStatus.Granted)
            {
                await DisplayAlert("Permiso requerido", "Se necesitan permisos para acceder a la cámara y almacenamiento.", "OK");
                return;
            }

            await CrossMedia.Current.Initialize();

            if (!CrossMedia.Current.IsCameraAvailable || !CrossMedia.Current.IsTakePhotoSupported)
            {
                await DisplayAlert("No Camera", ":( No camera available.", "OK");
                return;
            }

            var file = await CrossMedia.Current.TakePhotoAsync(new StoreCameraMediaOptions
            {
                Directory = "Sample",
                Name = $"{DateTime.UtcNow}.jpg"
            });

            if (file == null)
                return;

            _rutaImagen = file.Path;

            try
            {
                using (var inputStream = file.GetStream())
                using (var memoryStream = new MemoryStream())
                {
                    using (var skiaImage = SKBitmap.Decode(inputStream))
                    {
                        var resizedImage = ResizeImage(skiaImage, 800, 600);
                        using (var skiaImageResized = SKImage.FromBitmap(resizedImage))
                        using (var skiaImageData = skiaImageResized.Encode(SKEncodedImageFormat.Jpeg, 80))
                        {
                            skiaImageData.SaveTo(memoryStream);
                            _base64Image = Convert.ToBase64String(memoryStream.ToArray());


                            Console.WriteLine($"Base64 Image Length: {_base64Image.Length}");
                        }
                    }
                }

                imagen.Source = ImageSource.FromStream(() =>
                {
                    var stream = file.GetStream();
                    return stream;
                });


                Console.WriteLine($"Base64 Image Preview: {_base64Image.Substring(0, 100)}...");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Error al convertir la imagen a Base64: {ex.Message}", "OK");
            }
        }

        private SKBitmap ResizeImage(SKBitmap originalImage, int maxWidth, int maxHeight)
        {
            int width, height;
            if (originalImage.Width > originalImage.Height)
            {
                width = maxWidth;
                height = originalImage.Height * maxHeight / originalImage.Width;
            }
            else
            {
                height = maxHeight;
                width = originalImage.Width * maxWidth / originalImage.Height;
            }

            return originalImage.Resize(new SKImageInfo(width, height), SKFilterQuality.Medium);
        }

        private async void LoadLocationDataAsync()
        {
            try
            {
                var request = new GeolocationRequest(GeolocationAccuracy.Medium);
                var location = await Geolocation.GetLocationAsync(request);

                if (location != null)
                {
                    LongitudeEntry.Text = location.Longitude.ToString(CultureInfo.InvariantCulture);
                    LatitudeEntry.Text = location.Latitude.ToString(CultureInfo.InvariantCulture);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Error al obtener la ubicación: {ex.Message}", "OK");
            }
        }

        private async void OnViewListasitiosClicked(object sender, EventArgs e)
        {

            await Navigation.PushAsync(new ListaSitiosPage());
        }
    }
}
