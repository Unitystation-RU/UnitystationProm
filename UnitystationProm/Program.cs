﻿using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System;
using Prometheus;
using System.Text.Json;

namespace UnitystationProm
{
    class Program
    {
        private static readonly Gauge Players =
            Metrics.CreateGauge("unitystation_players", "Amount of players on server", new GaugeConfiguration
            {
                LabelNames = new[] { "server" }
            });
        private static readonly Gauge Fps =
            Metrics.CreateGauge("unitystation_fps", "Frames per second", new GaugeConfiguration
            {
                LabelNames = new[] { "server" }
            });
        private static readonly Gauge BuildVersion =
            Metrics.CreateGauge("unitystation_version", "Version of build", new GaugeConfiguration
            {
                LabelNames = new[] { "server" }
            });
        private static readonly Gauge InGameTime =
            Metrics.CreateGauge("unitystation_time", "In-game time", new GaugeConfiguration
            {
                LabelNames = new[] { "server" }
            });

        static async Task Main()
        {
            var server = new MetricServer(hostname: "*", port: 7776);
            Metrics.SuppressDefaultMetrics();
            var http = new HttpClient();
            server.Start();

            Metrics.DefaultRegistry.AddBeforeCollectCallback(async (cancel) =>
            {
                try
                {
                    Console.WriteLine("Scraped");
                    var res = await http.GetStringAsync("https://api.unitystation.org/serverlist");
                    var par = JsonSerializer.Deserialize<RootObject>(res);
                    var names = par.servers.Select(s => s.ServerName);
                    var oldNames = new Gauge[] {
                    Players,
                    Fps,
                    BuildVersion,
                    InGameTime
                    }
                        .SelectMany(name => name
                            .GetAllLabelValues()
                            .Select(v => v.FirstOrDefault()));

                    foreach (var old in oldNames.Except(names))
                    {
                        Players.RemoveLabelled(old);
                        Fps.RemoveLabelled(old);
                        BuildVersion.RemoveLabelled(old);
                        InGameTime.RemoveLabelled(old);
                    }

                    foreach (var server in par.servers)
                    {
                        Players.WithLabels(server.ServerName).Set(server.PlayerCount);
                        Fps.WithLabels(server.ServerName).Set(server.fps);
                        BuildVersion.WithLabels(server.ServerName).Set(server.BuildVersion);
                        InGameTime.WithLabels(server.ServerName).Set(DateTime.TryParse(server.IngameTime, out var time) ? time.TimeOfDay.TotalMinutes : 0);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            });

            Console.WriteLine("Started");
            while (true)
            {
                await Task.Delay(5000);
            }
        }
    }
}
