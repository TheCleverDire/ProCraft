// Part of fCraft | Copyright (c) 2009-2012 Matvei Stefarov <me@matvei.org> | BSD-3 | See LICENSE.txt
// Initial support contributed by Tyler Kennedy <tk@tkte.ch>
using System;
using System.IO;
using System.IO.Compression;
using JetBrains.Annotations;

namespace fCraft.MapConversion {
    /// <summary> MCSharp map conversion implementation, for converting MCSharp map format into fCraft's default map format. </summary>
    public class MapMCSharp : IMapImporter, IMapExporter {
        public virtual string ServerName {
            get { return "MCSharp, MCLawl, MCForge, FemtoCraft"; }
        }

        public virtual bool SupportsImport {
            get { return true; }
        }

        public virtual bool SupportsExport {
            get { return true; }
        }

        public virtual string FileExtension {
            get { return "lvl"; }
        }

        public virtual MapStorageType StorageType {
            get { return MapStorageType.SingleFile; }
        }

        public virtual MapFormat Format {
            get { return MapFormat.MCSharp; }
        }


        public virtual bool ClaimsName( string fileName ) {
            if( fileName == null ) throw new ArgumentNullException( "fileName" );
            return fileName.CaselessEnds( ".lvl" );
        }


        public virtual bool Claims( string fileName ) {
            if( fileName == null ) throw new ArgumentNullException( "fileName" );
            try {
                using( FileStream mapStream = File.OpenRead( fileName ) ) {
                    using( GZipStream gs = new GZipStream( mapStream, CompressionMode.Decompress ) ) {
                        BinaryReader bs = new BinaryReader( gs );
                        return ( bs.ReadUInt16() == 0x752 );
                    }
                }
            } catch( Exception ) {
                return false;
            }
        }


        public virtual Map LoadHeader( string fileName ) {
            if( fileName == null ) throw new ArgumentNullException( "fileName" );
            using( FileStream mapStream = File.OpenRead( fileName ) ) {
                using( GZipStream gs = new GZipStream( mapStream, CompressionMode.Decompress ) ) {
                    return LoadHeaderInternal( gs );
                }
            }
        }


        protected static Map LoadHeaderInternal( [NotNull] Stream stream ) {
            if( stream == null ) throw new ArgumentNullException( "stream" );
            BinaryReader bs = new BinaryReader( stream );

            // Read in the magic number
            if( bs.ReadUInt16() != 0x752 ) {
                throw new MapFormatException();
            }

            // Read in the map dimesions
            int width = bs.ReadUInt16();
            int length = bs.ReadUInt16();
            int height = bs.ReadUInt16();

            // ReSharper disable UseObjectOrCollectionInitializer
            Map map = new Map( null, width, length, height, false );
            // ReSharper restore UseObjectOrCollectionInitializer

            // Read in the spawn location
            map.Spawn = new Position {
                X = bs.ReadInt16() * 32,
                Z = bs.ReadInt16() * 32,
                Y = bs.ReadInt16() * 32,
                R = bs.ReadByte(),
                L = bs.ReadByte(),
            };

            stream.ReadByte(); // pervisit permission
            stream.ReadByte(); // perbuild permission
            return map;
        }


        public virtual Map Load( string fileName ) {
            if( fileName == null ) throw new ArgumentNullException( "fileName" );
            using( FileStream mapStream = File.OpenRead( fileName ) ) {
                using( GZipStream gs = new GZipStream( mapStream, CompressionMode.Decompress ) ) {

                    Map map = LoadHeaderInternal( gs );

                    // Read in the map data
                    map.Blocks = new byte[map.Volume];
                    BufferUtil.ReadAll( gs, map.Blocks );

                    map.ConvertBlockTypes( Mapping );

                    if( gs.ReadByte() != 0xBD ) return map;
                    ReadCustomBlocks( gs, map );
                    return map;
                }
            }
        }

        const byte customTile = 163;
        static void ReadCustomBlocks( Stream s, Map map) {
            byte[] chunk = new byte[16 * 16 * 16];
            byte[] data = new byte[1];
            
            for( int z = 0; z < map.Height; z += 16 )
                for( int y = 0; y < map.Length; y += 16 )
                    for( int x = 0; x < map.Width; x += 16 )
            {
                int read = s.Read( data, 0, 1 );
                if( read == 0 || data[0] != 1 ) continue;
                s.Read( chunk, 0, chunk.Length );
                
                int baseIndex = map.Index( x, y, z );
                for( int i = 0; i < chunk.Length; i++ ) {
                    int xx = i & 0xF, yy = (i >> 4) & 0xF, zz = (i >> 8) & 0xF;
                    int index = baseIndex + map.Index( xx, yy, zz );
                    
                    if (map.Blocks[index] != customTile) continue;
                    map.Blocks[index] = chunk[i];
                }
            }
        }


        public virtual bool Save( Map mapToSave, string fileName ) {
            if( mapToSave == null ) throw new ArgumentNullException( "mapToSave" );
            if( fileName == null ) throw new ArgumentNullException( "fileName" );
            using( FileStream mapStream = File.Create( fileName ) ) {
                using( GZipStream gs = new GZipStream( mapStream, CompressionMode.Compress ) ) {
                    BinaryWriter bs = new BinaryWriter( gs );
                    SaveHeader( mapToSave, bs );
                    
                    // Write the map data
                    bs.Write( mapToSave.Blocks, 0, mapToSave.Blocks.Length );
                    bs.Close();
                }
                return true;
            }
        }
        
        
        protected static void SaveHeader( Map mapToSave, BinaryWriter bs ) {
            // Write the magic number
            bs.Write( (ushort)0x752 );

            // Write the map dimensions
            bs.Write( (short)mapToSave.Width );
            bs.Write( (short)mapToSave.Length );
            bs.Write( (short)mapToSave.Height );

            // Write the spawn location
            bs.Write( (short)mapToSave.Spawn.BlockX );
            bs.Write( (short)mapToSave.Spawn.BlockZ );
            bs.Write( (short)mapToSave.Spawn.BlockY );

            //Write the spawn orientation
            bs.Write( mapToSave.Spawn.R );
            bs.Write( mapToSave.Spawn.L );

            // Write the VisitPermission and BuildPermission bytes
            bs.Write( (byte)0 );
            bs.Write( (byte)0 );
        }


        protected static readonly byte[] Mapping = new byte[256];

        static MapMCSharp() {
            Mapping[70] = (byte)Block.BrownMushroom;// flagbase
            Mapping[71] = (byte)Block.White;        // fallsnow
            Mapping[72] = (byte)Block.White;        // snow
            Mapping[73] = (byte)Block.StillLava;    // fastdeathlava
            Mapping[74] = (byte)Block.TNT;          // c4
            Mapping[75] = (byte)Block.Red;          // c4det

            // 76-79 unused
            Mapping[80] = (byte) Block.Cobblestone; // door_cobblestone
            // 81 = door_cobblestone_air
            
            // 82 unused
            Mapping[83] = (byte)Block.Red;          // door_red;
            // 84 = door_red_air
            Mapping[85] = (byte)Block.Orange;       // door_orange
            Mapping[86] = (byte)Block.Yellow;       // door_yellow
            Mapping[87] = (byte)Block.Lime;         // door_lightgreen
            
            // 88 unused
            Mapping[89] = (byte)Block.Teal;         // door_aquagreen
            Mapping[90] = (byte)Block.Cyan;         // door_cyan
            Mapping[91] = (byte)Block.Aqua;         // door_lightblue
            Mapping[92] = (byte)Block.Indigo;       // door_purple
            Mapping[93] = (byte)Block.Violet;       // door_lightpurple
            Mapping[94] = (byte)Block.Magenta;      // door_pink
            Mapping[95] = (byte)Block.Pink;         // door_darkpink
            Mapping[96] = (byte)Block.Black;        // door_darkgray
            Mapping[97] = (byte)Block.Gray;         // door_lightgray
            Mapping[98] = (byte)Block.White;        // door_white
            
            // 99 unused
            Mapping[100] = (byte)Block.Glass;       // op_glass
            Mapping[101] = (byte)Block.Obsidian;    // opsidian
            Mapping[102] = (byte)Block.Brick;       // op_brick
            Mapping[103] = (byte)Block.Stone;       // op_stone
            Mapping[104] = (byte)Block.Cobblestone; // op_cobblestone
            // 105 = op_air
            Mapping[106] = (byte)Block.Water;       // op_water

            // 107-109 unused
            Mapping[110] = (byte)Block.Wood;        // wood_float
            Mapping[111] = (byte)Block.Log;         // door
            Mapping[112] = (byte)Block.Lava;        // lava_fast
            Mapping[113] = (byte)Block.Obsidian;    // door2
            Mapping[114] = (byte)Block.Glass;       // door3
            Mapping[115] = (byte)Block.Stone;       // door4
            Mapping[116] = (byte)Block.Leaves;      // door5
            Mapping[117] = (byte)Block.Sand;        // door6
            Mapping[118] = (byte)Block.Wood;        // door7
            Mapping[119] = (byte)Block.Green;       // door8
            Mapping[120] = (byte)Block.TNT;         // door9
            Mapping[121] = (byte)Block.Slab;        // door10

            Mapping[122] = (byte)Block.Log;         // tdoor
            Mapping[123] = (byte)Block.Obsidian;    // tdoor2
            Mapping[124] = (byte)Block.Glass;       // tdoor3
            Mapping[125] = (byte)Block.Stone;       // tdoor4
            Mapping[126] = (byte)Block.Leaves;      // tdoor5
            Mapping[127] = (byte)Block.Sand;        // tdoor6
            Mapping[128] = (byte)Block.Wood;        // tdoor7
            Mapping[129] = (byte)Block.Green;       // tdoor8

            Mapping[130] = (byte)Block.White;       // MsgWhite
            Mapping[131] = (byte)Block.Black;       // MsgBlack
            Mapping[132] = (byte)Block.Air;         // MsgAir
            Mapping[133] = (byte)Block.Water;       // MsgWater
            Mapping[134] = (byte)Block.Lava;        // MsgLava

            Mapping[135] = (byte)Block.TNT;         // tdoor9
            Mapping[136] = (byte)Block.Slab;        // tdoor10
            Mapping[137] = (byte)Block.Air;         // tdoor11
            Mapping[138] = (byte)Block.Water;       // tdoor12
            Mapping[139] = (byte)Block.Lava;        // tdoor13

            Mapping[140] = (byte)Block.Water;       // WaterDown
            Mapping[141] = (byte)Block.Lava;        // LavaDown
            Mapping[143] = (byte)Block.Aqua;        // WaterFaucet
            Mapping[144] = (byte)Block.Orange;      // LavaFaucet

            // 143 unused
            Mapping[145] = (byte)Block.Water;       // finiteWater
            Mapping[146] = (byte)Block.Lava;        // finiteLava
            Mapping[147] = (byte)Block.Cyan;        // finiteFaucet

            Mapping[148] = (byte)Block.Log;         // odoor1
            Mapping[149] = (byte)Block.Obsidian;    // odoor2
            Mapping[150] = (byte)Block.Glass;       // odoor3
            Mapping[151] = (byte)Block.Stone;       // odoor4
            Mapping[152] = (byte)Block.Leaves;      // odoor5
            Mapping[153] = (byte)Block.Sand;        // odoor6
            Mapping[154] = (byte)Block.Wood;        // odoor7
            Mapping[155] = (byte)Block.Green;       // odoor8
            Mapping[156] = (byte)Block.TNT;         // odoor9
            Mapping[157] = (byte)Block.Slab;        // odoor10
            Mapping[158] = (byte)Block.Lava;        // odoor11
            Mapping[159] = (byte)Block.Water;       // odoor12

            Mapping[160] = (byte)Block.Air;         // air_portal
            Mapping[161] = (byte)Block.Water;       // water_portal
            Mapping[162] = (byte)Block.Lava;        // lava_portal

            Mapping[customTile] = customTile; // handled specially
            
            Mapping[164] = (byte)Block.Air;         // air_door
            Mapping[165] = (byte)Block.Air;         // air_switch
            Mapping[166] = (byte)Block.Water;       // water_door
            Mapping[167] = (byte)Block.Lava;        // lava_door

            // 168-174 = odoor*_air
            Mapping[175] = (byte)Block.Cyan;        // blue_portal
            Mapping[176] = (byte)Block.Orange;      // orange_portal
            // 177-181 = odoor*_air

            Mapping[182] = (byte)Block.TNT;         // smalltnt
            Mapping[183] = (byte)Block.TNT;         // bigtnt
            Mapping[184] = (byte)Block.Lava;        // tntexplosion
            Mapping[185] = (byte)Block.Lava;        // fire

            // 186 unused
            Mapping[187] = (byte)Block.Glass;       // rocketstart
            Mapping[188] = (byte)Block.Gold;        // rockethead
            Mapping[189] = (byte)Block.Iron;       // firework

            Mapping[190] = (byte)Block.Lava;        // deathlava
            Mapping[191] = (byte)Block.Water;       // deathwater
            Mapping[192] = (byte)Block.Air;         // deathair
            Mapping[193] = (byte)Block.Water;       // activedeathwater
            Mapping[194] = (byte)Block.Lava;        // activedeathlava

            Mapping[195] = (byte)Block.Lava;        // magma
            Mapping[196] = (byte)Block.Water;       // geyser

            // 197-210 = air
            Mapping[211] = (byte)Block.Red;         // door8_air
            Mapping[212] = (byte)Block.Lava;        // door9_air
            // 213-229 = air

            Mapping[230] = (byte)Block.Aqua;        // train
            Mapping[231] = (byte)Block.TNT;         // creeper
            Mapping[232] = (byte)Block.MossyCobble;  // zombiebody
            Mapping[233] = (byte)Block.Lime;        // zombiehead

            // 234 unused
            Mapping[235] = (byte)Block.White;       // birdwhite
            Mapping[236] = (byte)Block.Black;       // birdblack
            Mapping[237] = (byte)Block.Lava;        // birdlava
            Mapping[238] = (byte)Block.Red;         // birdred
            Mapping[239] = (byte)Block.Water;       // birdwater
            Mapping[240] = (byte)Block.Blue;        // birdblue
            Mapping[242] = (byte)Block.Lava;        // birdkill

            Mapping[245] = (byte)Block.Gold;        // fishgold
            Mapping[246] = (byte)Block.Sponge;      // fishsponge
            Mapping[247] = (byte)Block.Gray;        // fishshark
            Mapping[248] = (byte)Block.Red;         // fishsalmon
            Mapping[249] = (byte)Block.Blue;        // fishbetta
        }
    }
}