﻿// Copyright 2013 Matvei Stefarov <me@matvei.org>
using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using RgbColor = System.Drawing.Color;

namespace fCraft.Drawing
{
    /// <summary> Represents a palette of Minecraft blocks,
    /// that allows matching RGB colors to their closest block equivalents. </summary>
    public class BlockPalette : IEnumerable
    {
        // XN/YN/ZN are illuminant D65 tristimulus values
        const double XN = 95.047,
                     YN = 100.000,
                     ZN = 108.883;
        // these constant are used in CIEXYZ -> CIELAB conversion
        const double LinearThreshold = (6 / 29d) * (6 / 29d) * (6 / 29d),
                      LinearMultiplier = (1 / 3d) * (29 / 6d) * (29 / 6d),
                      LinearConstant = (4 / 29d);

        readonly Dictionary<LabColor, Block[]> palette = new Dictionary<LabColor, Block[]>();

        /// <summary> Name of this palette. </summary>
        public string Name { get; private set; }

        /// <summary> Number of block layers in this palette. FindBestMatch(...) will return an array of this size. </summary>
        public int Layers { get; private set; }

        /// <summary> Opacity level (between 0.0 and 1.0) below which pixel is considered transparent.
        /// Default is 0.2 (20% opaque / 80% transparent). </summary>
        public float TransparencyThreshold { get; private set; }
        const float TransparencyThresholdDefault = 0.2f;

        readonly Block[] transparent;

        protected BlockPalette([NotNull] string name, int layers)
        {
            if (name == null)
                throw new ArgumentNullException("name");
            Name = name;
            Layers = layers;

            TransparencyThreshold = TransparencyThresholdDefault;
            transparent = new Block[layers];
            for (int i = 0; i < layers; i++)
            {
                transparent[i] = Block.None;
            }
        }


        protected void Add(RgbColor color, [NotNull] Block[] blocks)
        {
            Add(RgbToLab(color, false), blocks);
        }
        
        protected void Add(RgbColor color, Block block)
        {
            Add(RgbToLab(color, false), new Block[] { block });
        }


        protected void Add(LabColor color, Block[] blocks)
        {
            if (blocks == null)
            {
                throw new ArgumentNullException("blocks");
            }
            if (blocks.Length != Layers)
            {
                throw new ArgumentException("Number of blocks must match number the of layers.");
            }
            palette.Add(color, blocks);
        }


        [NotNull]
        public Block[] FindBestMatch(RgbColor color)
        {
            if (color.A < TransparencyThreshold * 255)
            {
                return transparent;
            }
            LabColor pixelColor = RgbToLab(color, true);
            double closestDistance = double.MaxValue;
            Block[] bestMatch = null;
            foreach (var pair in palette)
            {
                double distance = ColorDifference(pixelColor, pair.Key);
                if (distance < closestDistance)
                {
                    bestMatch = pair.Value;
                    closestDistance = distance;
                }
            }
            if (bestMatch == null)
            {
                throw new InvalidOperationException("Could not find match: palette is empty!");
            }
            return bestMatch;
        }


        // CIE76 formula for Delta-E, over CIELAB color space
        static double ColorDifference(LabColor color1, LabColor color2)
        {
            return (color2.L - color1.L) * (color2.L - color1.L) * 1.2 +
                   (color2.a - color1.a) * (color2.a - color1.a) +
                   (color2.b - color1.b) * (color2.b - color1.b);
        }


        // Conversion from RGB to CIELAB, using illuminant D65.
        static LabColor RgbToLab(RgbColor col, bool adjustContrast)
        {
            // RGB are assumed to be in [0...255] range
            double R = col.R / 255d, G = col.G / 255d, B = col.B / 255d;

            // CIEXYZ coordinates are normalized to [0...1]
            double x = 0.4124564 * R + 0.3575761 * G + 0.1804375 * B;
            double y = 0.2126729 * R + 0.7151522 * G + 0.0721750 * B;
            double z = 0.0193339 * R + 0.1191920 * G + 0.9503041 * B;

            x /= XN; y /= YN; z /= ZN;
            x = XyzToLab(x); y = XyzToLab(y); z = XyzToLab(z);

            LabColor result = new LabColor
            {
                // L is normalized to [0...100]
                L = 116 * y - 16,
                a = 500 * (x - y),
                b = 200 * (y - z)
            };
            
            if (adjustContrast) result.L *= .75;
            return result;
        }


        static double XyzToLab(double ratio)
        {
            if (ratio > LinearThreshold)
                return Math.Pow(ratio, 1 / 3d);
            else
                return LinearMultiplier * ratio + LinearConstant;
        }


        // we have to implement IEnumerable to be able to use the collection initializers
        IEnumerator IEnumerable.GetEnumerator()
        {
            return palette.GetEnumerator();
        }


        #region Standard Patterns
        
        public static List<BlockPalette> Palettes = new List<BlockPalette>() {
            DefineLight(), DefineLight2(), DefineDark(), DefineDark2(),
            DefineBW(), DefineGray(), DefineDarkGray(), 
            DefineLayered(), DefineLayered2(),  DefineLayeredGray(),
        };
        
        public static BlockPalette FindPalette(string name) {
            foreach (BlockPalette palette in Palettes) {
                if (palette.Name.CaselessEquals(name)) return palette;
            }
            return null;
        }

        // 1-layer standard blocks, lit
        static BlockPalette DefineLight()
        {
            return new BlockPalette("Light", 1) {
                { RgbColor.FromArgb(109, 80, 57), Block.Dirt },
                { RgbColor.FromArgb(176, 170, 130), Block.Sand },
                { RgbColor.FromArgb(111, 104, 104), Block.Gravel },
                { RgbColor.FromArgb(179, 44, 44), Block.Red },
                { RgbColor.FromArgb(179, 111, 44), Block.Orange },
                { RgbColor.FromArgb(179, 179, 44), Block.Yellow },
                { RgbColor.FromArgb(109, 179, 44), Block.Lime },
                { RgbColor.FromArgb(44, 179, 44), Block.Green },
                { RgbColor.FromArgb(44, 179, 111), Block.Teal },
                { RgbColor.FromArgb(44, 179, 179), Block.Aqua },
                { RgbColor.FromArgb(86, 132, 179), Block.Cyan },
                { RgbColor.FromArgb(99, 99, 180), Block.Blue },
                { RgbColor.FromArgb(111, 44, 180), Block.Indigo },
                { RgbColor.FromArgb(141, 62, 179), Block.Violet },
                { RgbColor.FromArgb(180, 44, 180), Block.Magenta },
                { RgbColor.FromArgb(179, 44, 111), Block.Pink },
                { RgbColor.FromArgb(64, 64, 64), Block.Black },
                { RgbColor.FromArgb(118, 118, 118), Block.Gray },
                { RgbColor.FromArgb(179, 179, 179), Block.White },
                { RgbColor.FromArgb(21, 19, 29), Block.Obsidian }
            };
        }

        // 1-layer standard+CPE blocks, lit
        static BlockPalette DefineLight2() {
            return new BlockPalette("Light2", 1) {
                {RgbColor.FromArgb( 124, 124, 124 ), Block.Stone},//
                {RgbColor.FromArgb( 125, 89, 61 ), Block.Dirt},//
                {RgbColor.FromArgb( 97, 97, 97 ), Block.Admincrete},//
                {RgbColor.FromArgb( 157, 128, 79 ), Block.Wood},//
                {RgbColor.FromArgb( 211, 204, 151 ), Block.Sand},//
                {RgbColor.FromArgb( 128, 124, 122 ), Block.Gravel},//
                {RgbColor.FromArgb( 92, 72, 43 ), Block.Log},//
                {RgbColor.FromArgb( 188, 190, 61 ), Block.Sponge},//
                {RgbColor.FromArgb( 195, 44, 44 ), Block.Red},//
                {RgbColor.FromArgb( 195, 119, 44 ), Block.Orange},//
                {RgbColor.FromArgb( 195, 195, 44 ), Block.Yellow},//
                {RgbColor.FromArgb( 119, 195, 44 ), Block.Lime},//
                {RgbColor.FromArgb( 44, 195, 44 ), Block.Green},//
                {RgbColor.FromArgb( 44, 195, 119 ), Block.Teal},//
                {RgbColor.FromArgb( 44, 195, 195 ), Block.Aqua},//
                {RgbColor.FromArgb( 91, 143, 195 ), Block.Cyan},//
                {RgbColor.FromArgb( 105, 105, 195 ), Block.Blue},//
                {RgbColor.FromArgb( 119, 44, 195 ), Block.Indigo},//
                {RgbColor.FromArgb( 152, 65, 195 ), Block.Violet},//
                {RgbColor.FromArgb( 195, 44, 195 ), Block.Magenta},//
                {RgbColor.FromArgb( 195, 44, 119 ), Block.Pink},//
                {RgbColor.FromArgb( 68, 68, 68 ), Block.Black},//
                {RgbColor.FromArgb( 125, 125, 125 ), Block.Gray},//
                {RgbColor.FromArgb( 195, 195, 195 ), Block.White},//
                {RgbColor.FromArgb( 252, 242, 83 ), Block.Gold},//
                {RgbColor.FromArgb( 234, 234, 234 ), Block.Iron},//
                {RgbColor.FromArgb( 185, 110, 98 ), Block.Brick},//
                {RgbColor.FromArgb( 23, 20, 34 ), Block.Obsidian},//
                {RgbColor.FromArgb( 221, 214, 165 ), Block.Sandstone},//
                {RgbColor.FromArgb( 191, 116, 136 ), Block.LightPink},//
                {RgbColor.FromArgb( 49, 68, 21 ), Block.DarkGreen},//
                {RgbColor.FromArgb( 75, 45, 25 ), Block.Brown},//
                {RgbColor.FromArgb( 34, 45, 135 ), Block.DarkBlue},//
                {RgbColor.FromArgb( 34, 102, 131 ), Block.Turquoise},//
                {RgbColor.FromArgb( 216, 219, 237 ), Block.Tile},//
                {RgbColor.FromArgb( 232, 228, 220 ), Block.Pillar}//
            };
        }


        // 1-layer standard blocks, shadowed
        static BlockPalette DefineDark()
        {
            return new BlockPalette("Dark", 1) {
                { RgbColor.FromArgb(67, 50, 37), Block.Dirt },
                { RgbColor.FromArgb(108, 104, 80), Block.Sand },
                { RgbColor.FromArgb(68, 64, 64), Block.Gravel },
                { RgbColor.FromArgb(109, 28, 28), Block.Red },
                { RgbColor.FromArgb(110, 70, 31), Block.Orange },
                { RgbColor.FromArgb(109, 109, 29), Block.Yellow },
                { RgbColor.FromArgb(68, 109, 29), Block.Lime },
                { RgbColor.FromArgb(28, 109, 31), Block.Green },
                { RgbColor.FromArgb(28, 109, 69), Block.Teal },
                { RgbColor.FromArgb(28, 109, 108), Block.Aqua },
                { RgbColor.FromArgb(53, 81, 109), Block.Cyan },
                { RgbColor.FromArgb(61, 61, 109), Block.Blue },
                { RgbColor.FromArgb(68, 28, 109), Block.Indigo },
                { RgbColor.FromArgb(87, 40, 110), Block.Violet },
                { RgbColor.FromArgb(109, 28, 110), Block.Magenta },
                { RgbColor.FromArgb(109, 29, 69), Block.Pink },
                { RgbColor.FromArgb(41, 41, 41), Block.Black },
                { RgbColor.FromArgb(72, 72, 72), Block.Gray },
                { RgbColor.FromArgb(109, 109, 109), Block.White },
                { RgbColor.FromArgb(15, 14, 20), Block.Obsidian }
            };
        }

        // 1-layer standard+CPE blocks, shadowed
        static BlockPalette DefineDark2() {
            return new BlockPalette("Dark2", 1) {
                {RgbColor.FromArgb( 74, 74, 74 ), Block.Stone},//
                {RgbColor.FromArgb( 75, 53, 37 ), Block.Dirt},//
                {RgbColor.FromArgb( 58, 58, 58 ), Block.Admincrete},//
                {RgbColor.FromArgb( 94, 77, 47 ), Block.Wood},//
                {RgbColor.FromArgb( 127, 122, 91 ), Block.Sand},//
                {RgbColor.FromArgb( 77, 74, 73 ), Block.Gravel},//
                {RgbColor.FromArgb( 55, 43, 26 ), Block.Log},//
                {RgbColor.FromArgb( 113, 114, 37 ), Block.Sponge},//
                {RgbColor.FromArgb( 117, 26, 26 ), Block.Red},//
                {RgbColor.FromArgb( 117, 71, 26 ), Block.Orange},//
                {RgbColor.FromArgb( 117, 117, 26 ), Block.Yellow},//
                {RgbColor.FromArgb( 71, 117, 26 ), Block.Lime},//
                {RgbColor.FromArgb( 26, 117, 26 ), Block.Green},//
                {RgbColor.FromArgb( 26, 117, 71 ), Block.Teal},//
                {RgbColor.FromArgb( 26, 117, 117 ), Block.Aqua},//
                {RgbColor.FromArgb( 91, 86, 117 ), Block.Cyan},//
                {RgbColor.FromArgb( 63, 63, 117 ), Block.Blue},//
                {RgbColor.FromArgb( 71, 26, 117 ), Block.Indigo},//
                {RgbColor.FromArgb( 91, 39, 117 ), Block.Violet},//
                {RgbColor.FromArgb( 117, 26, 117 ), Block.Magenta},//
                {RgbColor.FromArgb( 117, 26, 71 ), Block.Pink},//
                {RgbColor.FromArgb( 41, 41, 41 ), Block.Black},//
                {RgbColor.FromArgb( 75, 75, 75 ), Block.Gray},//
                {RgbColor.FromArgb( 117, 117, 117 ), Block.White},//
                {RgbColor.FromArgb( 151, 145, 50 ), Block.Gold},//
                {RgbColor.FromArgb( 140, 140, 140 ), Block.Iron},//
                {RgbColor.FromArgb( 111, 66, 59 ), Block.Brick},//
                {RgbColor.FromArgb( 14, 12, 20 ), Block.Obsidian},//
                {RgbColor.FromArgb( 133, 129, 99 ), Block.Sandstone},//
                {RgbColor.FromArgb( 115, 67, 82 ), Block.LightPink},//
                {RgbColor.FromArgb( 29, 41, 13 ), Block.DarkGreen},//
                {RgbColor.FromArgb( 45, 27, 15 ), Block.Brown},//
                {RgbColor.FromArgb( 20, 27, 81 ), Block.DarkBlue},//
                {RgbColor.FromArgb( 20, 61, 79 ), Block.Turquoise},//
                {RgbColor.FromArgb( 130, 131, 142 ), Block.Tile},//
                {RgbColor.FromArgb( 139, 137, 132 ), Block.Pillar}//
            };
        }


        // 2-layer standard blocks
        static BlockPalette DefineLayered()
        {
            BlockPalette palette = new BlockPalette("Layered", 2);
            foreach (var pair in DefineLight().palette) {
                palette.Add(pair.Key, new[] { Block.None, pair.Value[0] });
            }
            foreach (var pair in DefineDark().palette) {
                palette.Add(pair.Key, new[] { pair.Value[0], Block.Air });
            }
            
            palette.Add(RgbColor.FromArgb(61, 74, 167), new[] { Block.White, Block.StillWater });
            palette.Add(RgbColor.FromArgb(47, 59, 152), new[] { Block.Gray, Block.StillWater });
            palette.Add(RgbColor.FromArgb(34, 47, 140), new[] { Block.Black, Block.StillWater });
            palette.Add(RgbColor.FromArgb(22, 38, 131), new[] { Block.Obsidian, Block.StillWater });
            return palette;
        }

        // 2-layer standard+CPE blocks
        static BlockPalette DefineLayered2() {
            BlockPalette palette = new BlockPalette("Layered2", 2);
            foreach (var pair in DefineLight2().palette) {
                palette.Add(pair.Key, new[] { Block.None, pair.Value[0] });
            }
            foreach (var pair in DefineDark2().palette) {
                palette.Add(pair.Key, new[] { pair.Value[0], Block.Air });
            }
            
            palette.Add(RgbColor.FromArgb(61, 74, 167), new[] { Block.White, Block.StillWater });
            palette.Add(RgbColor.FromArgb(47, 59, 152), new[] { Block.Gray, Block.StillWater });
            palette.Add(RgbColor.FromArgb(34, 47, 140), new[] { Block.Black, Block.StillWater });
            palette.Add(RgbColor.FromArgb(22, 38, 131), new[] { Block.Obsidian, Block.StillWater });
            return palette;
        }


        // 1-layer gray blocks, lit
        static BlockPalette DefineGray()
        {
            return new BlockPalette("Gray", 1) {
                {RgbColor.FromArgb( 64, 64, 64 ), Block.Black},
                {RgbColor.FromArgb( 118, 118, 118 ), Block.Gray},
                {RgbColor.FromArgb( 179, 179, 179 ), Block.White},
                {RgbColor.FromArgb( 21, 19, 29 ), Block.Obsidian}
            };
        }


        // 2-layer gray blocks, shadowed
        static BlockPalette DefineDarkGray()
        {
            return new BlockPalette("DarkGray", 1) {
                {RgbColor.FromArgb( 41, 41, 41 ), Block.Black},
                {RgbColor.FromArgb( 72, 72, 72 ), Block.Gray},
                {RgbColor.FromArgb( 109, 109, 109 ), Block.White},
                {RgbColor.FromArgb( 15, 14, 20 ), Block.Obsidian}
            };
        }


        // 2-layer gray blocks
        static BlockPalette DefineLayeredGray()
        {
            BlockPalette palette = new BlockPalette("LayeredGray", 2);
            foreach (var pair in DefineGray().palette) {
                palette.Add(pair.Key, new[] { Block.None, pair.Value[0] });
            }
            foreach (var pair in DefineDarkGray().palette) {
                palette.Add(pair.Key, new[] { pair.Value[0], Block.Air });
            }
            return palette;
        }


        // "black" (obsidian) and white blocks only
        static BlockPalette DefineBW()
        {
            return new BlockPalette("BW", 1) {
                {RgbColor.FromArgb( 179, 179, 179 ), Block.White},
                {RgbColor.FromArgb( 21, 19, 29 ), Block.Obsidian}
            };
        }

        #endregion

        protected struct LabColor
        {
            public double L, a, b;
        }
    }
}