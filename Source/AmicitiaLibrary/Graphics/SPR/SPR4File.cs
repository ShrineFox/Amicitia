﻿using System.Text;

namespace AmicitiaLibrary.Graphics.SPR
{
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.InteropServices;
    using AmicitiaLibrary.Utilities;
    using System.Linq;
    using IO;
    using TGA;

    [StructLayout(LayoutKind.Explicit, Size = SIZE)]
    internal struct Spr4Header
    {
        public const ushort FLAGS = 0x0001;
        public const string TAG = "SPR4";
        public const int SIZE = 0x20;

        [FieldOffset(0)]
        public ushort flags;

        [FieldOffset(2)]
        public ushort userId;

        [FieldOffset(4)]
        public int reserved1;

        [FieldOffset(8)]
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 5)]
        public string tag;

        [FieldOffset(12)]
        public int headerSize;

        [FieldOffset(16)]
        public int deprecatedSize;

        [FieldOffset(20)]
        public ushort numTextures;

        [FieldOffset(22)]
        public ushort numKeyFrames;

        [FieldOffset(24)]
        public int texturePointerTableOffset;

        [FieldOffset(28)]
        public int keyFramePointerTableOffset;

        public Spr4Header(int textureCount, int keyFrameCount)
        {
            flags = FLAGS;
            userId = 0;
            reserved1 = 0;
            tag = TAG;
            headerSize = SIZE;
            deprecatedSize = 0;
            numTextures = (ushort)textureCount;
            numKeyFrames = (ushort)keyFrameCount;
            texturePointerTableOffset = 0;
            keyFramePointerTableOffset = 0;
        }
    }

    public class Spr4File : BinaryBase, ISprFile
    {
        private IList<TgaFile> mTextures;
        private IList<SprSprite> mKeyFrames;

        internal Spr4File(BinaryReader reader)
        {
            Read(reader);
        }

        public Spr4File()
        {
            mTextures = new List<TgaFile>();
            mKeyFrames = new List<SprSprite>();
        }

        public Spr4File(IList<TgaFile> textures, IList<SprSprite> keyframes)
        {
            mTextures = textures;
            mKeyFrames = keyframes;
        }

        public Spr4File(Stream stream, bool leaveOpen = false)
        {
            using (var reader = new BinaryReader(stream, Encoding.Default, leaveOpen))
                Read(reader);
        }

        public int TextureCount
        {
            get { return mTextures.Count; }
        }

        public int KeyFrameCount
        {
            get { return mKeyFrames.Count; }
        }

        public IList<TgaFile> Textures
        {
            get { return mTextures; }
        }

        public IList<SprSprite> Sprites
        {
            get { return mKeyFrames; }
        }

        IList<ITextureFile> ISprFile.Textures => Textures.Cast<ITextureFile>().ToList();

        public static Spr4File Load(string path)
        {
            using (BinaryReader reader = new BinaryReader(File.OpenRead(path), System.Text.Encoding.Default, true))
                return new Spr4File(reader);
        }

        public static Spr4File LoadFrom(Stream stream, bool leaveStreamOpen = false)
        {
            using (BinaryReader reader = new BinaryReader(stream, System.Text.Encoding.Default, leaveStreamOpen))
                return new Spr4File(reader);
        }

        internal override void Write(BinaryWriter writer)
        {
            // Save the start position to calculate the filesize and 
            // to write out the header after we know where all the structure offsets are
            Stream stream = writer.BaseStream;
            int posFileStart = (int)stream.Position;
            stream.Seek(Spr4Header.SIZE, SeekOrigin.Current);

            // Create initial header and tables
            Spr4Header header = new Spr4Header(TextureCount, KeyFrameCount);
            TypePointerTable[] keyFramePointerTable = new TypePointerTable[header.numKeyFrames];
            TypePointerTable[] texturePointerTable = new TypePointerTable[header.numTextures];

            // Set the pointer table offsets and seek past the entries
            // as the entries will be written later
            header.texturePointerTableOffset = (int)(stream.Position - posFileStart);
            stream.Seek(TypePointerTable.SIZE * header.numTextures, SeekOrigin.Current);

            header.keyFramePointerTableOffset = (int)(stream.Position - posFileStart);
            stream.Seek(TypePointerTable.SIZE * header.numKeyFrames, SeekOrigin.Current);

            // Write out the keyframe data and fill up the pointer table
            for (int i = 0; i < header.numKeyFrames; i++)
            {
                writer.AlignPosition(16);
                keyFramePointerTable[i].offset = (int)(stream.Position - posFileStart);
                mKeyFrames[i].Write(writer);
            }

            writer.Seek(16, SeekOrigin.Current);
            writer.AlignPosition(16);

            // Write out the texture data and fill up the pointer table
            for (int i = 0; i < header.numTextures; i++)
            {
                texturePointerTable[i].offset = (int)(stream.Position - posFileStart);
                mTextures[i].Write(writer);
                writer.Seek(16, SeekOrigin.Current);
            }

            // Save the end position
            long posFileEnd = stream.Position;

            // Seek back to the tables and write out the tables
            stream.Seek(posFileStart + header.texturePointerTableOffset, SeekOrigin.Begin);
            stream.WriteStructures(texturePointerTable, header.numTextures);

            stream.Seek(posFileStart + header.keyFramePointerTableOffset, SeekOrigin.Begin);
            stream.WriteStructures(keyFramePointerTable, header.numKeyFrames);

            // Seek back to the file and write out the header with
            // the offsets to the structures in the file
            writer.BaseStream.Seek(posFileStart, SeekOrigin.Begin);
            writer.BaseStream.WriteStructure(header);

            // Set the file pointer back to the end of the file as expected
            writer.BaseStream.Seek(posFileEnd, SeekOrigin.Begin);
        }

        // Private Methods
        private void Read(BinaryReader reader)
        {
            Stream stream = reader.BaseStream;
            int posFileStart = (int)reader.GetPosition();

            Spr4Header header = stream.ReadStructure<Spr4Header>();

            stream.Seek(posFileStart + header.texturePointerTableOffset, SeekOrigin.Begin);
            TypePointerTable[] texturePointerTable = stream.ReadStructures<TypePointerTable>(header.numTextures);

            stream.Seek(posFileStart + header.keyFramePointerTableOffset, SeekOrigin.Begin);
            TypePointerTable[] keyFramePointerTable = stream.ReadStructures<TypePointerTable>(header.numKeyFrames);

            mTextures = new List<TgaFile>(header.numTextures);
            for (int i = 0; i < header.numTextures; i++)
            {
                stream.Seek(posFileStart + texturePointerTable[i].offset, SeekOrigin.Begin);
                mTextures.Add(new TgaFile(stream, true));
            }

            mKeyFrames = new List<SprSprite>(header.numKeyFrames);
            for (int i = 0; i < header.numKeyFrames; i++)
            {
                stream.Seek(posFileStart + keyFramePointerTable[i].offset, SeekOrigin.Begin);
                mKeyFrames.Add(new SprSprite(reader));
            }
        }
    }
}

