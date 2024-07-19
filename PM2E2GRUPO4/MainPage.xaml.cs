using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Newtonsoft.Json;
using Plugin.Media;
using Plugin.Media.Abstractions;
using Plugin.Maui.Audio;
using System.Timers;
using System.Globalization;

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
            if (_audioRecorder.IsRecording)
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

                await DisplayAlert("Audio Guardado", $"Audio guardado y enviado a la API.", "OK");
                FileNameEntry.Text = string.Empty;
                DescriptionEntry.Text = string.Empty;
                StartRecordingButton.IsVisible = true;
                StopRecordingButton.IsVisible = false;
                ResumeRecordingButton.IsVisible = false;
                _timer.Stop();
                RecordingTimeLabel.Text = "00:00";
                _secondsElapsed = 0;
            }
        }

    
        

        private async void OnViewAudioListClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new AudioListPage());
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

            using (var memoryStream = new MemoryStream())
            {
                using (var fileStream = file.GetStream())
                {
                    await fileStream.CopyToAsync(memoryStream);
                }

                byte[] imageBytes = memoryStream.ToArray();
                _base64Image = Convert.ToBase64String(imageBytes);
            }

            imagen.Source = ImageSource.FromStream(() =>
            {
                var stream = file.GetStream();
                return stream;
            });
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
                    audiofile = base64Audio,
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
                        await DisplayAlert("Error", $"Error al enviar información a la API. StatusCode: {response.StatusCode}", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Excepción: {ex.Message}", "OK");
            }
        }



        /*private async Task SendAudioInfoToApi(string base64Audio)
        {
            try
            {
                string description = DescriptionEntry.Text.Trim();

                var audioInfo = new
                {
                    audiofile = base64Audio,
                    fecha = DateTime.Now.ToString("yyyy-MM-dd"),
                    descripcion = description,
                    longitud = LongitudeEntry.Text.Trim(),
                    latitud = LatitudeEntry.Text.Trim(),
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
                        await DisplayAlert("Error", $"Error al enviar información a la API. StatusCode: {response.StatusCode}", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Excepción: {ex.Message}", "OK");
            }
        }*/

        private async void LoadLocationDataAsync()
        {
            try
            {
                var locationPermissionStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (locationPermissionStatus != PermissionStatus.Granted)
                {
                    await DisplayAlert("Permiso denegado", "Se necesita permiso para acceder a la ubicación.", "OK");
                    return;
                }

                var location = await Geolocation.GetLastKnownLocationAsync();

                if (location == null)
                {
                    location = await Geolocation.GetLocationAsync(new GeolocationRequest
                    {
                        DesiredAccuracy = GeolocationAccuracy.Medium,
                        Timeout = TimeSpan.FromSeconds(30)
                    });

                    if (location == null)
                    {
                        await DisplayAlert("Ubicación no disponible", "No se pudo obtener la ubicación actual.", "OK");
                        return;
                    }
                }

                LongitudeEntry.Text = location.Longitude.ToString();
                LatitudeEntry.Text = location.Latitude.ToString();
            }
            catch (FeatureNotSupportedException)
            {
                await DisplayAlert("Error", "La geolocalización no está soportada en este dispositivo.", "OK");
            }
            catch (PermissionException)
            {
                await DisplayAlert("Error", "Permiso de ubicación denegado.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Error al obtener ubicación: {ex.Message}", "OK");
            }
        }

        private void OnTimedEvent(object sender, ElapsedEventArgs e)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                _secondsElapsed++;
                RecordingTimeLabel.Text = TimeSpan.FromSeconds(_secondsElapsed).ToString(@"mm\:ss");
            });
        }
    }
}
