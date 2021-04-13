using Microsoft.Azure.Management.Media.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace MigrationMediaService.Classes
{
    public static class MediaTransform
    {
        public static string Name = Environment.GetEnvironmentVariable("MediaTransformName");
        public static StandardEncoderPreset Preset = new StandardEncoderPreset(
        codecs: new Codec[]
        {
            new AacAudio(
                channels: 2,
                samplingRate: 48000,
                bitrate: 128000,
                profile: AacAudioProfile.AacLc
            ),

            // https://support.google.com/youtube/answer/2853702?hl=id
            new H264Video(
                layers:  new H264Layer[]
                {
                    new H264Layer // Resolution: 1280x720
                    {
                        Bitrate=1800000,
                        Width="1280",
                        Height="720",
                        Label="HD",
                    },
                    //new H264Layer // YouTube 144p: 256×360 // 360p
                    //{
                    //    //Bitrate=1000000,
                    //    Width="640",
                    //    Height="360",
                    //    Label="MD",
                    //},
                    new H264Layer // YouTube 144p: 256×144
                    {
                        Bitrate=64000,
                        Width="256",
                        Height="144",
                        Label="SD",
                    }
                }),

            new JpgImage(
                start: "{Best}",
                layers: new JpgLayer[] {
                    new JpgLayer(
                        width: "100%",
                        height: "100%"
                    ),
                })
        },
        formats: new Format[]
        {
            new Mp4Format(
                filenamePattern:"Video-{Basename}-{Label}-{Bitrate}{Extension}"
            ),
            new JpgFormat(
                filenamePattern:"Thumbnail-{Basename}-{Index}{Extension}"
            )
        });
    }
}
