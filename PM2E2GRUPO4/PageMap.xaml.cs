using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;


namespace PM2E2GRUPO4
{

	public partial class PageMap : ContentPage
	{
        private double latitude;
        private double longitude;
        private string description;
        private string fotografia;

        public PageMap(double latitude, double longitude, string description, string fotografia)
		{
			InitializeComponent();

            this.latitude = latitude;
            this.longitude = longitude;
            this.description = description;
            this.fotografia = fotografia;


            var pin = new Pin
            {
                Label = description,
                Address = description,
                Type = PinType.Place,
                Location = new Location(latitude, longitude)
            };

            pin.MarkerClicked += OnPinClicked;

            map.Pins.Add(pin);
            map.MoveToRegion(MapSpan.FromCenterAndRadius(new Location(latitude, longitude), Distance.FromMiles(1)));
        }

        private async void OnPinClicked(object sender, PinClickedEventArgs e)
        {
            e.HideInfoWindow = true;
            var pin = sender as Pin;
            if (pin != null)
            {
                await DisplayAlert("Pin Information", $"{pin.Label}\nLocation: {latitude}, {longitude}", "OK");
            }
        }

        private async void ShareButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                var imageSource = ConvertBase64ToImageSource(fotografia);

                if (imageSource != null)
                {
                    var imagePath = await SaveImageAsync(imageSource);

                    var shareTitle = $"Foto de {description} en ({latitude}, {longitude})";
                    var shareMessage = $"Compartiendo foto de {description} en la ubicación ({latitude}, {longitude})";

                    await DisplayAlert("Compartir Foto", shareMessage, "OK");

                    await Share.RequestAsync(new ShareFileRequest
                    {
                        Title = shareTitle,
                        File = new ShareFile(imagePath),
                    });
                }
                else
                {
                    await DisplayAlert("Error", "No se pudo convertir la imagen para compartir.", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Error al compartir: {ex.Message}", "OK");
            }
        }

        private async Task<string> SaveImageAsync(ImageSource imageSource)
        {
            var fileName = $"temp_image_{Guid.NewGuid()}.png";

            byte[] imageBytes;
            using (var memoryStream = new MemoryStream())
            {
                var stream = await ((StreamImageSource)imageSource).Stream(CancellationToken.None);
                await stream.CopyToAsync(memoryStream);
                imageBytes = memoryStream.ToArray();
            }

            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

            File.WriteAllBytes(filePath, imageBytes);

            return filePath;
        }

        private ImageSource ConvertBase64ToImageSource(string base64String)
        {
            try
            {
                byte[] imageBytes = Convert.FromBase64String(base64String);
                return ImageSource.FromStream(() => new MemoryStream(imageBytes));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al convertir Base64 a ImageSource: {ex.Message}");
                return null;
            }
        }

        private void NavigateButton_Clicked(object sender, EventArgs e)
        {
            var searchQuery = $"{latitude},{longitude}";
            var searchUrl = $"https://www.google.com/maps/search/?api=1&query={searchQuery}";

            Launcher.OpenAsync(new Uri(searchUrl));
        }
    }
}