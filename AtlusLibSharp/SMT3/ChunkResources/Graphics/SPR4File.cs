﻿namespace AtlusLibSharp.SMT3.ChunkResources.Graphics
{
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.InteropServices;
    using Utilities;

    [StructLayout(LayoutKind.Explicit, Size = SIZE)]
    internal struct SPR4Header
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

        public SPR4Header(int textureCount, int keyFrameCount)
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

    public class SPR4File : BinaryFileBase
    {
        // Private Fields
        private byte[][] _tgaTextures;
        private SPRKeyFrame[] _keyFrames;

        // Constructors
        internal SPR4File(BinaryReader reader)
        {
            InternalRead(reader);
        }

        public SPR4File(IList<SPRKeyFrame> keyFrames, IList<byte[]> tgaTextures)
        {
            _tgaTextures = new byte[tgaTextures.Count][];
            _keyFrames = new SPRKeyFrame[keyFrames.Count];
            tgaTextures.CopyTo(_tgaTextures, 0);
            keyFrames.CopyTo(_keyFrames, 0);
        }

        // Properties
        public int TGATextureCount
        {
            get { return _tgaTextures.Length; }
        }

        public int KeyFrameCount
        {
            get { return _keyFrames.Length; }
        }

        public byte[][] TGATextures
        {
            get { return _tgaTextures; }
        }

        public SPRKeyFrame[] KeyFrames
        {
            get { return _keyFrames; }
        }

        // Public Methods
        public static SPR4File LoadFrom(string path)
        {
            using (BinaryReader reader = new BinaryReader(File.OpenRead(path), System.Text.Encoding.Default, true))
                return new SPR4File(reader);
        }

        public static SPR4File LoadFrom(Stream stream, bool leaveStreamOpen)
        {
            using (BinaryReader reader = new BinaryReader(stream, System.Text.Encoding.Default, leaveStreamOpen))
                return new SPR4File(reader);
        }

        // Internal Methods
        internal override void InternalWrite(BinaryWriter writer)
        {
            // Save the start position to calculate the filesize and 
            // to write out the header after we know where all the structure offsets are
            Stream stream = writer.BaseStream;
            int posFileStart = (int)stream.Position;
            stream.Seek(SPR4Header.SIZE, SeekOrigin.Current);

            // Create initial header and tables
            SPR4Header header = new SPR4Header(TGATextureCount, KeyFrameCount);
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
                _keyFrames[i].InternalWrite(writer);
            }

            // Write out the texture data and fill up the pointer table
            for (int i = 0; i < header.numTextures; i++)
            {
                writer.AlignPosition(16);
                texturePointerTable[i].offset = (int)(stream.Position - posFileStart);
                writer.Write(_tgaTextures[i]);
            }

            // Write out padding at the end of the file
            writer.AlignPosition(64);

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
        private void InternalRead(BinaryReader reader)
        {
            Stream stream = reader.BaseStream;
            int posFileStart = (int)reader.GetPosition();

            SPR4Header header = stream.ReadStructure<SPR4Header>();

            stream.Seek(posFileStart + header.texturePointerTableOffset, SeekOrigin.Begin);
            TypePointerTable[] texturePointerTable = stream.ReadStructures<TypePointerTable>(header.numTextures);

            stream.Seek(posFileStart + header.keyFramePointerTableOffset, SeekOrigin.Begin);
            TypePointerTable[] keyFramePointerTable = stream.ReadStructures<TypePointerTable>(header.numKeyFrames);

            _tgaTextures = new byte[header.numTextures][];
            for (int i = 0; i < header.numTextures; i++)
            {
                stream.Seek(posFileStart + texturePointerTable[i].offset, SeekOrigin.Begin);
                int texSize = ((i == header.numTextures - 1) ? (int)reader.BaseStream.Length : texturePointerTable[i + 1].offset) - texturePointerTable[i].offset;
                _tgaTextures[i] = reader.ReadBytes(texSize);
            }

            _keyFrames = new SPRKeyFrame[header.numKeyFrames];
            for (int i = 0; i < header.numKeyFrames; i++)
            {
                stream.Seek(posFileStart + keyFramePointerTable[i].offset, SeekOrigin.Begin);
                _keyFrames[i] = new SPRKeyFrame(reader);
            }
        }
    }
}
