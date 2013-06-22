using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ExifLib
{
    /// <summary>
    /// A class for reading Exif data from a JPEG file. The file will be open for reading for as long as the class exists.
    /// <seealso cref="http://gvsoft.homedns.org/exif/Exif-explanation.html"/>
    /// </summary>
    public sealed class ExifReader : IDisposable
    {
        private readonly Stream _stream;
        private readonly BinaryReader _reader;
        private static readonly Regex _nullDateTimeMatcher = new Regex(@"^[\s0]{4}[:\s][\s0]{2}[:\s][\s0]{5}[:\s][\s0]{2}[:\s][\s0]{2}$");

        /// <summary>
        /// The main tag id/absolute file offset catalogue
        /// </summary>
        private Dictionary<ushort, long> ifd0Catalogue;

        /// <summary>
        /// The thumbnail tag id/absolute file offset catalogue
        /// </summary>
        /// <remarks>JPEG images contain 2 main sections - one for the main image (which contains most of the useful EXIF data), and one for the thumbnail
        /// image (which contains little more than the thumbnail itself). This catalogue is only used by <see cref="GetJpegThumbnailBytes"/>.</remarks>
        private Dictionary<ushort, long> ifd1Catalogue;

        /// <summary>
        /// Indicates whether to read data using big or little endian byte aligns
        /// </summary>
        private bool isLittleEndian;

        /// <summary>
        /// The position in the filestream at which the TIFF header starts
        /// </summary>
        private long tiffHeaderStart;

        /// <summary>
        /// The location of the thumbnail IFD
        /// </summary>
        private uint ifd1Offset;

        public ExifReader(string fileName) : this(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
        }

        public ExifReader(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");

            if (!stream.CanSeek)
                throw new ExifLibException("ExifLib requires a seekable stream");

            // JPEG encoding uses big endian (i.e. Motorola) byte aligns. The TIFF encoding
            // found later in the document will specify the byte aligns used for the
            // rest of the document.
            isLittleEndian = false;

            try
            {
                // Open the file in a stream
                _stream = stream;
                _reader = new BinaryReader(_stream);

                // Make sure the file's a JPEG.
                if (ReadUShort() != 0xFFD8)
                    throw new ExifLibException("File is not a valid JPEG");

                // Scan to the start of the Exif content
                ReadToExifStart();

                // Create an index of all Exif tags found within the document
                CreateTagIndex();
            }
            catch
            {
                // If instantiation fails, make sure there's no mess left behind
                Dispose();

                throw;
            }
        }

        #region TIFF methods

        /// <summary>
        /// Returns the length (in bytes) per component of the specified TIFF data type
        /// </summary>
        /// <returns></returns>
        private byte GetTIFFFieldLength(ushort tiffDataType)
        {
            switch (tiffDataType)
            {
                case 1:
                case 2:
                case 7:
                case 6:
                    return 1;
                case 3:
                case 8:
                    return 2;
                case 4:
                case 9:
                case 11:
                    return 4;
                case 5:
                case 10:
                case 12:
                    return 8;
                default:
                    throw new ExifLibException(string.Format("Unknown TIFF datatype: {0}", tiffDataType));
            }
        }

        #endregion

        #region Methods for reading data directly from the filestream

        /// <summary>
        /// Gets a 2 byte unsigned integer from the file
        /// </summary>
        /// <returns></returns>
        private ushort ReadUShort()
        {
            return ToUShort(ReadBytes(2));
        }

        /// <summary>
        /// Gets a 4 byte unsigned integer from the file
        /// </summary>
        /// <returns></returns>
        private uint ReadUint()
        {
            return ToUint(ReadBytes(4));
        }

        private string ReadString(int chars)
        {
            var bytes = ReadBytes(chars);
            return Encoding.UTF8.GetString(bytes, 0, bytes.Length);
        }

        private byte[] ReadBytes(int byteCount)
        {
            return _reader.ReadBytes(byteCount);
        }

        /// <summary>
        /// Reads some bytes from the specified TIFF offset
        /// </summary>
        /// <param name="tiffOffset"></param>
        /// <param name="byteCount"></param>
        /// <returns></returns>
        private byte[] ReadBytes(ushort tiffOffset, int byteCount)
        {
            // Keep the current file offset
            long originalOffset = _stream.Position;

            // Move to the TIFF offset and retrieve the data
            _stream.Seek(tiffOffset + tiffHeaderStart, SeekOrigin.Begin);

            byte[] data = _reader.ReadBytes(byteCount);

            // Restore the file offset
            _stream.Position = originalOffset;

            return data;
        }

        #endregion

        #region Data conversion methods for interpreting datatypes from a byte array

        /// <summary>
        /// Converts 2 bytes to a ushort using the current byte aligns
        /// </summary>
        /// <returns></returns>
        private ushort ToUShort(byte[] data)
        {
            if (isLittleEndian != BitConverter.IsLittleEndian)
                Array.Reverse(data);

            return BitConverter.ToUInt16(data, 0);
        }

        /// <summary>
        /// Converts 8 bytes to an unsigned rational using the current byte aligns.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        /// <seealso cref="ToRational"/>
        private double ToURational(byte[] data)
        {
            var numeratorData = new byte[4];
            var denominatorData = new byte[4];

            Array.Copy(data, numeratorData, 4);
            Array.Copy(data, 4, denominatorData, 0, 4);

            uint numerator = ToUint(numeratorData);
            uint denominator = ToUint(denominatorData);

            return numerator/(double) denominator;
        }

        /// <summary>
        /// Converts 8 bytes to a signed rational using the current byte aligns.
        /// </summary>
        /// <remarks>
        /// A TIFF rational contains 2 4-byte integers, the first of which is
        /// the numerator, and the second of which is the denominator.
        /// </remarks>
        /// <param name="data"></param>
        /// <returns></returns>
        private double ToRational(byte[] data)
        {
            var numeratorData = new byte[4];
            var denominatorData = new byte[4];

            Array.Copy(data, numeratorData, 4);
            Array.Copy(data, 4, denominatorData, 0, 4);

            int numerator = ToInt(numeratorData);
            int denominator = ToInt(denominatorData);

            return numerator/(double) denominator;
        }

        /// <summary>
        /// Converts 4 bytes to a uint using the current byte aligns
        /// </summary>
        /// <returns></returns>
        private uint ToUint(byte[] data)
        {
            if (isLittleEndian != BitConverter.IsLittleEndian)
                Array.Reverse(data);

            return BitConverter.ToUInt32(data, 0);
        }

        /// <summary>
        /// Converts 4 bytes to an int using the current byte aligns
        /// </summary>
        /// <returns></returns>
        private int ToInt(byte[] data)
        {
            if (isLittleEndian != BitConverter.IsLittleEndian)
                Array.Reverse(data);

            return BitConverter.ToInt32(data, 0);
        }

        private double ToDouble(byte[] data)
        {
            if (isLittleEndian != BitConverter.IsLittleEndian)
                Array.Reverse(data);

            return BitConverter.ToDouble(data, 0);
        }

        private float ToSingle(byte[] data)
        {
            if (isLittleEndian != BitConverter.IsLittleEndian)
                Array.Reverse(data);

            return BitConverter.ToSingle(data, 0);
        }

        private short ToShort(byte[] data)
        {
            if (isLittleEndian != BitConverter.IsLittleEndian)
                Array.Reverse(data);

            return BitConverter.ToInt16(data, 0);
        }

        private sbyte ToSByte(byte[] data)
        {
            // An sbyte should just be a byte with an offset range.
            return (sbyte) (data[0] - byte.MaxValue);
        }

        /// <summary>
        /// Retrieves an array from a byte array using the supplied converter
        /// to read each individual element from the supplied byte array
        /// </summary>
        /// <param name="data"></param>
        /// <param name="elementLengthBytes"></param>
        /// <param name="converter"></param>
        /// <returns></returns>
        private static Array GetArray<T>(byte[] data, int elementLengthBytes, ConverterMethod<T> converter)
        {
            Array convertedData = new T[data.Length/elementLengthBytes];

            var buffer = new byte[elementLengthBytes];

            // Read each element from the array
            for (int elementCount = 0; elementCount < data.Length/elementLengthBytes; elementCount++)
            {
                // Place the data for the current element into the buffer
                Array.Copy(data, elementCount*elementLengthBytes, buffer, 0, elementLengthBytes);

                // Process the data and place it into the output array
                convertedData.SetValue(converter(buffer), elementCount);
            }

            return convertedData;
        }

        /// <summary>
        /// A delegate used to invoke any of the data conversion methods
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        /// <remarks>Although this could be defined as covariant, it wouldn't work on Windows Phone</remarks>
        private delegate T ConverterMethod<T>(byte[] data);

        #endregion

        #region Stream seek methods - used to get to locations within the JPEG

        /// <summary>
        /// Scans to the Exif block
        /// </summary>
        private void ReadToExifStart()
        {
            // The file has a number of blocks (Exif/JFIF), each of which
            // has a tag number followed by a length. We scan the document until the required tag (0xFFE1)
            // is found. All tags start with FF, so a non FF tag indicates an error.

            // Get the next tag.
            byte markerStart;
            byte markerNumber = 0;
            while (((markerStart = _reader.ReadByte()) == 0xFF) && (markerNumber = _reader.ReadByte()) != 0xE1)
            {
                // Get the length of the data.
                ushort dataLength = ReadUShort();

                // Jump to the end of the data (note that the size field includes its own size)!
                _stream.Seek(dataLength - 2, SeekOrigin.Current);
            }

            // It's only success if we found the 0xFFE1 marker
            if (markerStart != 0xFF || markerNumber != 0xE1)
                throw new ExifLibException("Could not find Exif data block");
        }

        /// <summary>
        /// Reads through the Exif data and builds an index of all Exif tags in the document
        /// </summary>
        /// <returns></returns>
        private void CreateTagIndex()
        {
            // The next 4 bytes are the size of the Exif data.
            ReadUShort();

            // Next is the Exif data itself. It starts with the ASCII "Exif" followed by 2 zero bytes.
            if (ReadString(4) != "Exif")
                throw new ExifLibException("Exif data not found");

            // 2 zero bytes
            if (ReadUShort() != 0)
                throw new ExifLibException("Malformed Exif data");

            // We're now into the TIFF format
            tiffHeaderStart = _stream.Position;

            // What byte align will be used for the TIFF part of the document? II for Intel, MM for Motorola
            isLittleEndian = ReadString(2) == "II";

            // Next 2 bytes are always the same.
            if (ReadUShort() != 0x002A)
                throw new ExifLibException("Error in TIFF data");

            // Get the offset to the IFD (image file directory)
            uint ifdOffset = ReadUint();

            // Note that this offset is from the first byte of the TIFF header. Jump to the IFD.
            _stream.Position = ifdOffset + tiffHeaderStart;

            // Catalogue this first IFD (there will be another IFD)
            ifd0Catalogue = new Dictionary<ushort, long>();
            CatalogueIFD(ref ifd0Catalogue);

            // The address to the IFD1 (the thumbnail IFD) is located immediately after the main IFD
            ifd1Offset = ReadUint();

            // There's more data stored in the subifd, the offset to which is found in tag 0x8769.
            // As with all TIFF offsets, it will be relative to the first byte of the TIFF header.
            uint offset;
            if (!GetTagValue(ifd0Catalogue, 0x8769, out offset))
                throw new ExifLibException("Unable to locate Exif data");

            // Jump to the exif SubIFD
            _stream.Position = offset + tiffHeaderStart;

            // Add the subIFD to the catalogue too
            CatalogueIFD(ref ifd0Catalogue);

            // Go to the GPS IFD and catalogue that too. It's an optional
            // section.
            if (GetTagValue(ifd0Catalogue, 0x8825, out offset))
            {
                // Jump to the GPS SubIFD
                _stream.Position = offset + tiffHeaderStart;

                // Add the subIFD to the catalogue too
                CatalogueIFD(ref ifd0Catalogue);
            }

            // Finally, catalogue the thumbnail IFD if it's present
            if (ifd1Offset != 0)
            {
                _stream.Position = ifd1Offset + tiffHeaderStart;
                ifd1Catalogue = new Dictionary<ushort, long>();
                CatalogueIFD(ref ifd1Catalogue);
            }
        }
        #endregion

        #region Exif data catalog and retrieval methods

        public bool GetTagValue<T>(ExifTags tag, out T result)
        {
            return GetTagValue((ushort) tag, out result);
        }

        public bool GetTagValue<T>(ushort tagID, out T result)
        {
            // All useful EXIF tags are stored in the ifd0 catalogue. The ifd1 catalogue is only for thumbnail retrieval.
            return GetTagValue(ifd0Catalogue, tagID, out result);
        }

        /// <summary>
        /// Retrieves an Exif value with the requested tag ID
        /// </summary>
        private bool GetTagValue<T>(Dictionary<ushort, long> tagDictionary, ushort tagID, out T result)
        {
            ushort tiffDataType;
            uint numberOfComponents;
            byte[] tagData = GetTagBytes(tagDictionary, tagID, out tiffDataType, out numberOfComponents);

            if (tagData == null)
            {
                result = default(T);
                return false;
            }

            byte fieldLength = GetTIFFFieldLength(tiffDataType);

            // Convert the data to the appropriate datatype. Note the weird boxing via object.
            // The compiler doesn't like it otherwise.
            switch (tiffDataType)
            {
                case 1:
                    // unsigned byte
                    if (numberOfComponents == 1)
                        result = (T) (object) tagData[0];
                    else
                        result = (T) (object) tagData;
                    return true;
                case 2:
                    // ascii string
                    string str = Encoding.UTF8.GetString(tagData, 0, tagData.Length);

                    // There may be a null character within the string
                    int nullCharIndex = str.IndexOf('\0');
                    if (nullCharIndex != -1)
                        str = str.Substring(0, nullCharIndex);

                    // Special processing for dates.
                    if (typeof (T) == typeof (DateTime))
                    {
                        DateTime dateResult;
                        bool success = ToDateTime(str, out dateResult);
                        
                        result = (T)(object) dateResult;
                        return success;

                    }

                    result = (T) (object) str;
                    return true;
                case 3:
                    // unsigned short
                    if (numberOfComponents == 1)
                        result = (T) (object) ToUShort(tagData);
                    else
                        result = (T) (object) GetArray(tagData, fieldLength, ToUShort);
                    return true;
                case 4:
                    // unsigned long
                    if (numberOfComponents == 1)
                        result = (T) (object) ToUint(tagData);
                    else
                        result = (T) (object) GetArray(tagData, fieldLength, ToUint);
                    return true;
                case 5:
                    // unsigned rational
                    if (numberOfComponents == 1)
                        result = (T) (object) ToURational(tagData);
                    else
                        result = (T) (object) GetArray(tagData, fieldLength, ToURational);
                    return true;
                case 6:
                    // signed byte
                    if (numberOfComponents == 1)
                        result = (T) (object) ToSByte(tagData);
                    else
                        result = (T) (object) GetArray(tagData, fieldLength, ToSByte);
                    return true;
                case 7:
                    // undefined. Treat it as a byte.
                    if (numberOfComponents == 1)
                        result = (T) (object)tagData[0];
                    else
                        result = (T) (object) tagData;
                    return true;
                case 8:
                    // Signed short
                    if (numberOfComponents == 1)
                        result = (T) (object) ToShort(tagData);
                    else
                        result = (T) (object) GetArray(tagData, fieldLength, ToShort);
                    return true;
                case 9:
                    // Signed long
                    if (numberOfComponents == 1)
                        result = (T) (object) ToInt(tagData);
                    else
                        result = (T) (object) GetArray(tagData, fieldLength, ToInt);
                    return true;
                case 10:
                    // signed rational
                    if (numberOfComponents == 1)
                        result = (T) (object) ToRational(tagData);
                    else
                        result = (T) (object) GetArray(tagData, fieldLength, ToRational);
                    return true;
                case 11:
                    // single float
                    if (numberOfComponents == 1)
                        result = (T) (object) ToSingle(tagData);
                    else
                        result = (T) (object) GetArray(tagData, fieldLength, ToSingle);
                    return true;
                case 12:
                    // double float
                    if (numberOfComponents == 1)
                        result = (T) (object) ToDouble(tagData);
                    else
                        result = (T) (object) GetArray(tagData, fieldLength, ToDouble);
                    return true;
                default:
                    throw new ExifLibException(string.Format("Unknown TIFF datatype: {0}", tiffDataType));
            }
        }

        private static bool ToDateTime(string str, out DateTime result)
        {
            // From page 28 of the Exif 2.2 spec (http://www.exif.org/Exif2-2.PDF): 

            // "When the field is left blank, it is treated as unknown ... When the date and time are unknown, 
            // all the character spaces except colons (":") may be filled with blank characters"
            if (string.IsNullOrEmpty(str) || _nullDateTimeMatcher.IsMatch(str))
            {
                result = DateTime.MinValue;
                return false;
            }

            // There are 2 types of date - full date/time stamps, and plain dates. Dates are 10 characters long.
            if (str.Length == 10)
            {
                result = DateTime.ParseExact(str, "yyyy:MM:dd", CultureInfo.InvariantCulture);
                return true;
            }

            // "The format is "YYYY:MM:DD HH:MM:SS" with time shown in 24-hour format, and the date and time separated by one blank character [20.H].
            result = DateTime.ParseExact(str, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture);
            return true;
        }

        /// <summary>
        /// Gets the data in the specified tag ID, starting from before the IFD block.
        /// </summary>
        /// <param name="tiffDataType"></param>
        /// <param name="numberOfComponents">The number of items which make up the data item - i.e. for a string, this will be the
        /// number of characters in the string</param>
        /// <param name="tagDictionary"></param>
        /// <param name="tagID"></param>
        private byte[] GetTagBytes(Dictionary<ushort, long> tagDictionary, ushort tagID, out ushort tiffDataType, out uint numberOfComponents)
        {
            // Get the tag's offset from the catalogue and do some basic error checks
            if (_stream == null || _reader == null || tagDictionary == null || !tagDictionary.ContainsKey(tagID))
            {
                tiffDataType = 0;
                numberOfComponents = 0;
                return null;
            }

            long tagOffset = tagDictionary[tagID];

            // Jump to the TIFF offset
            _stream.Position = tagOffset;

            // Read the tag number from the file
            ushort currentTagID = ReadUShort();

            if (currentTagID != tagID)
                throw new ExifLibException("Tag number not at expected offset");

            // Read the offset to the Exif IFD
            tiffDataType = ReadUShort();
            numberOfComponents = ReadUint();
            byte[] tagData = ReadBytes(4);

            // If the total space taken up by the field is longer than the
            // 2 bytes afforded by the tagData, tagData will contain an offset
            // to the actual data.
            var dataSize = (int) (numberOfComponents*GetTIFFFieldLength(tiffDataType));

            if (dataSize > 4)
            {
                ushort offsetAddress = ToUShort(tagData);
                return ReadBytes(offsetAddress, dataSize);
            }

            // The value is stored in the tagData starting from the left
            Array.Resize(ref tagData, dataSize);

            return tagData;
        }

        /// <summary>
        /// Records all Exif tags and their offsets within
        /// the file from the current IFD
        /// </summary>
        private void CatalogueIFD(ref Dictionary<ushort, long> tagOffsets)
        {
            // Assume we're just before the IFD.

            // First 2 bytes is the number of entries in this IFD
            ushort entryCount = ReadUShort();

            for (ushort currentEntry = 0; currentEntry < entryCount; currentEntry++)
            {
                ushort currentTagNumber = ReadUShort();

                // Record this in the catalogue
                tagOffsets[currentTagNumber] = _stream.Position - 2;

                // Go to the end of this item (10 bytes, as each entry is 12 bytes long)
                _stream.Seek(10, SeekOrigin.Current);
            }
        }

        #endregion

        #region Thumbnail retrieval
        /// <summary>
        /// Retrieves a JPEG thumbnail from the image if one is present. Note that this method cannot retrieve thumbnails encoded in other formats,
        /// but since the DCF specification specifies that thumbnails must be JPEG, this method will be sufficient for most purposes
        /// See http://gvsoft.homedns.org/exif/exif-explanation.html#TIFFThumbs or http://partners.adobe.com/public/developer/en/tiff/TIFF6.pdf for 
        /// details on the encoding of TIFF thumbnails
        /// </summary>
        /// <returns></returns>
        public byte[] GetJpegThumbnailBytes()
        {
            if (ifd1Catalogue == null)
                return null;

            // Get the thumbnail encoding
            ushort compression;
            if (!GetTagValue(ifd1Catalogue, (ushort) ExifTags.Compression, out compression))
                return null;

            // This method only handles JPEG thumbnails (compression type 6)
            if (compression != 6)
                return null;

            // Get the location of the thumbnail
            uint offset;
            if (!GetTagValue(ifd1Catalogue, (ushort) ExifTags.JPEGInterchangeFormat, out offset))
                return null;

            // Get the length of the thumbnail data
            uint length;
            if (!GetTagValue(ifd1Catalogue, (ushort) ExifTags.JPEGInterchangeFormatLength, out length))
                return null;

            _stream.Position = offset;

            // The thumbnail may be padded, so we scan forward until we reach the JPEG header (0xFFD8) or the end of the file
            int currentByte;
            int previousByte = -1;
            while ((currentByte = _stream.ReadByte()) != -1)
            {
                if (previousByte == 0xFF && currentByte == 0xD8)
                    break;

                previousByte = currentByte;

            }

            if (currentByte != 0xD8)
                return null;

            // Step back to the start of the JPEG header
            _stream.Position -= 2;

            var imageBytes = new byte[length];
            _stream.Read(imageBytes, 0, (int) length);

            // A valid JPEG stream ends with 0xFFD9
            if (imageBytes.Length < 2 || imageBytes[length - 1] != 0xD9 || imageBytes[length - 2] != 0xFF)
                return null;

            return imageBytes;
        }
        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            // Make sure the file handle is released
            if (_reader != null)
                _reader.Close();
            if (_stream != null)
                _stream.Close();
        }

        #endregion
    }

    public class ExifLibException : Exception
    {
        public ExifLibException()
        {
        }

        public ExifLibException(string message) : base(message)
        {
        }

        public ExifLibException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}