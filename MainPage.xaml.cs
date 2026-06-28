using FIRE;
using Microsoft.Maui.Controls;
using System;

namespace FIRE // GANTI INI
{
    public partial class MainPage : ContentPage
    {
        public MainPage() { InitializeComponent(); }

        private async void OnLoginClicked(object sender, EventArgs e)
        {
            string user = "";
            string pass = "";

            if (DeviceInfo.Idiom == DeviceIdiom.Phone)
            {
                user = EntryUsernameMobile.Text?.Trim() ?? "";
                pass = EntryPasswordMobile.Text ?? "";
            }
            else
            {
                user = EntryUsername.Text?.Trim() ?? "";
                pass = EntryPassword.Text ?? "";
            }

            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
            {
                await DisplayAlert(
                    "Ditolak",
                    "Username dan password wajib diisi!",
                    "OK"
                );

                return;
            }

            UserAccount? dbUser =
                await FireDatabase.Instance.GetUserByUsernameAsync(user);

            if (dbUser == null)
            {
                await DisplayAlert(
                    "Ditolak",
                    "Username tidak ditemukan di database!",
                    "OK"
                );

                return;
            }

            bool passwordValid =
                FireDatabase.VerifyPassword(pass, dbUser.PasswordHash);

            if (!passwordValid)
            {
                await DisplayAlert(
                    "Ditolak",
                    "Password salah!",
                    "OK"
                );

                return;
            }

            Preferences.Set("session_username", dbUser.Username);
            Preferences.Set("session_role", dbUser.Role);

            Application.Current!.MainPage =
                new FaceVerificationPage(dbUser.Username, dbUser.Role);
        }

        private void OnGoToRegisterClicked(object sender, EventArgs e) => Application.Current.MainPage = new RegisterPage();
    }
}