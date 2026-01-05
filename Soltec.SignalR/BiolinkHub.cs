using Soltec.SignalR.Properties;
using Microsoft.AspNet.SignalR;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;


namespace Soltec.SignalR
{
	public partial class BiolinkHub : Hub
	{




        public async Task SendSpeechText(string text)
        {
            Console.WriteLine($"Texto recibido: {text}");
            // Aquí puedes guardar, procesar, o retransmitir el texto
            await Clients.All.SendAsync(text); // Reenvío opcional
        }

        public void GetTest(string guid)
        {
            try
            {
                Clients.Caller.showTest("Resultado de la prueba realizada.");
            }
            catch (Exception ex)
            {
                Clients.Caller.fail(ex.Message);
                Console.WriteLine("Error Hub.GetFingerLicences: " + ex.Message);
            }
        }

        public async Task ReceiveAudio(string base64Audio)
        {
            byte[] audioBytes = Convert.FromBase64String(base64Audio);
            string tempFile = "C:\\Humberto\\Test.mp3";
            //if (File.Exists(tempFile))
            //    File.Delete(tempFile);

            File.WriteAllBytes(tempFile, audioBytes);

            string transcription = RunWhisperCpp(tempFile);
            File.Delete(tempFile);

            await Clients.Caller.sendAsync(transcription);
        }

        private string RunWhisperCpp(string filePath)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "C:\\Humberto\\whisper.cpp\\build\\bin\\Release\\whisper-cli.exe",
                    Arguments = $" -m C:\\Humberto\\whisper.cpp\\models\\ggml-base.en.bin -f {filePath} -otxt",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit();

            var resultFile = filePath + ".txt";//Path.ChangeExtension(filePath, ".txt");
            return File.Exists(resultFile) ? File.ReadAllText(resultFile) : "Error transcribiendo";
        }


        private readonly string tempDir = @"C:\Humberto\audio\tmp";
        public async Task SendAudio(string base64Audio)
        {
            Directory.CreateDirectory(tempDir);

            var webmPath = Path.Combine(tempDir, $"{Guid.NewGuid()}.webm");
            var mp3Path = Path.ChangeExtension(webmPath, ".wav");

            // Guardar .webm
            var bytes = Convert.FromBase64String(base64Audio);
            File.WriteAllBytes(webmPath, bytes);

            // Convertir a .mp3 con ffmpeg
            var success = ConvertToMp3(webmPath, mp3Path);
            if (!success)
            {
                await Clients.Caller.SendAsync("Error al convertir audio.");
                return;
            }

            // Leer y enviar MP3 como base64
            var mp3Bytes = File.ReadAllBytes(mp3Path);
            var mp3Base64 = Convert.ToBase64String(mp3Bytes);

            string transcription = RunWhisperCpp(mp3Path);

            await Clients.Caller.SendAsync(transcription);

            File.Delete(webmPath);
            File.Delete(mp3Path);
        }

        private bool ConvertToMp3(string input, string output)
        {
            var ffmpeg = @"C:\ffmpeg\ffmpeg-7.1.1-essentials_build\bin\ffmpeg.exe"; // asegúrate de que esté en el PATH o usa ruta completa
            var args = $"-i {input} -ar 16000 -ac 1 -f wav {output} -y";

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });

            process.WaitForExit();
            return File.Exists(output);
        }

    }
}
