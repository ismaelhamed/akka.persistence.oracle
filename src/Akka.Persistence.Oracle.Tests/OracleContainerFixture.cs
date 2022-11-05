//-----------------------------------------------------------------------
// <copyright file="OracleContainerFixture.cs" company="Akka.NET Project">
//     Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;
using Xunit;

namespace Akka.Persistence.Oracle.Tests
{
    [CollectionDefinition("OracleSpec")]
    public sealed class OracleSpecsFixture : ICollectionFixture<OracleContainerFixture> { }

    public class OracleContainerFixture : IAsyncLifetime
    {
        protected readonly string OracleContainerName = $"Oracle-{Guid.NewGuid():N}";
        protected DockerClient Client;

        protected static string ImageName => "gvenzl/oracle-xe";
        protected static string Tag => "21-slim";
        protected static string OracleImageName => $"{ImageName}:{Tag}";

        public OracleContainerFixture()
        {
            DockerClientConfiguration config;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                config = new DockerClientConfiguration(new Uri("unix://var/run/docker.sock"));
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                config = new DockerClientConfiguration(new Uri("npipe://./pipe/docker_engine"));
            else
                throw new NotSupportedException($"Unsupported OS [{RuntimeInformation.OSDescription}]");

            Client = config.CreateClient();
        }

        public async Task InitializeAsync()
        {
            var images = await Client.Images.ListImagesAsync(new ImagesListParameters
            {
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    {
                        "reference",
                        new Dictionary<string, bool>
                        {
                            { OracleImageName, true }
                        }
                    }
                }
            });

            if (images.Count == 0)
            {
                await Client.Images.CreateImageAsync(
                    new ImagesCreateParameters { FromImage = ImageName, Tag = Tag }, null,
                    new Progress<JSONMessage>(message =>
                    {
                        Console.WriteLine(!string.IsNullOrEmpty(message.ErrorMessage)
                            ? message.ErrorMessage
                            : $"{message.ID} {message.Status} {message.ProgressMessage}");
                    }));
            }

            // create the container
            await Client.Containers.CreateContainerAsync(new CreateContainerParameters
            {
                Image = OracleImageName,
                Name = OracleContainerName,
                Tty = false,
                ExposedPorts = new Dictionary<string, EmptyStruct>
                {
                    { "1521/tcp", new EmptyStruct() }
                },
                HostConfig = new HostConfig
                {
                    PortBindings = new Dictionary<string, IList<PortBinding>>
                    {
                        {
                            "1521/tcp",
                            new List<PortBinding>
                            {
                                new PortBinding { HostIP = "127.0.0.1", HostPort = "1521" }
                            }
                        }
                    }
                },
                Env = new[]
                {
                    "ORACLE_PASSWORD=oracle"
                }
            });

            // start the container
            await Client.Containers.StartContainerAsync(OracleContainerName, new ContainerStartParameters());

            // provide a 40 second startup delay since Oracle is way to slow to initialize
            await Task.Delay(TimeSpan.FromSeconds(40));
        }

        public async Task DisposeAsync()
        {
            if (Client == null)
                return;

            await Client.Containers.StopContainerAsync(OracleContainerName, new ContainerStopParameters());
            await Client.Containers.RemoveContainerAsync(OracleContainerName, new ContainerRemoveParameters { Force = true });
            Client.Dispose();
        }
    }
}
