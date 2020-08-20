using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ArtNet.Rdm
{
    /// <summary>
    /// 设备 UID 信息
    /// </summary>
    public class UId:IComparable
    {
        protected UId()
        {
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="manufacturerId">制造商 ID</param>
        /// <param name="deviceId">设备 ID</param>
        public UId(ushort manufacturerId,uint deviceId)
        {
            ManufacturerId = manufacturerId;
            DeviceId = deviceId;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="source">UID 信息</param>
        public UId(UId source)
        {
            ManufacturerId = source.ManufacturerId;
            DeviceId = source.DeviceId;
        }

        /// <summary>
        /// 构造函数（从指定的 ID 中创建 设备 UID）
        /// </summary>
        /// <param name="manufacturerId">制造商 ID</param>
        /// <param name="productId">产品 ID</param>
        /// <param name="deviceCode">设备 ID</param>
        public UId(ushort manufacturerId, byte productId, uint deviceCode)
            : this(manufacturerId, (uint)((productId << 24) + (deviceCode & 0x00FFFFFF)))
        {
        }

        /// <summary>
        /// 制造商 ID
        /// </summary>
        public ushort ManufacturerId { get; protected set; }

        /// <summary>
        /// 设备 ID
        /// </summary>
        public uint DeviceId { get; protected set; }

        #region 预定义值

        private static UId broadcast = new UId(0xFFFF, 0xFFFFFFFF);

        public static UId Broadcast
        {
            get { return broadcast; }
        }

        private static UId empty = new UId();

        public static UId Empty
        {
            get { return empty; }
        }

        public static UId ManfacturerBroadcast(ushort manufacturerId)
        {
            return new UId(manufacturerId, 0xFFFFFFFF);
        }

        private static UId minValue = new UId(0x1, 0x0);

        /// <summary>
        /// 获取最小 UID
        /// </summary>
        public static UId MinValue
        {
            get { return minValue; }
        }

        private static UId maxValue = new UId(0x7FFF, 0xFFFFFFFF);

        /// <summary>
        /// 获取最大 UID
        /// </summary>
        public static UId MaxValue
        {
            get { return maxValue; }
        }

        #endregion
        
        /// <summary>
        /// 将 UID 转为 16 进制字符串
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            // 将 制造商ID 转为 4bti 字符串，设备ID 转为 8bit 字符串
            return string.Format("{0}:{1}", ManufacturerId.ToString("X4"), DeviceId.ToString("X8"));
        }

        /// <summary>
        /// 生成 UID（随机生成 设备ID）
        /// </summary>
        /// <param name="manufacturerId">制造商 ID</param>
        /// <returns>UID</returns>
        public static UId NewUId(ushort manufacturerId)
        {
            Random randomId = new Random();
            return new UId(manufacturerId, (uint) randomId.Next(1,0x7FFFFFFF));
        }

        /// <summary>
        /// 生成 UID （随机生成 设备ID）
        /// </summary>
        /// <param name="manufacturerId">制造商 ID</param>
        /// <param name="productId">产品 ID</param>
        /// <returns>UID</returns>
        public static UId NewUId(ushort manufacturerId, byte productId)
        {
            Random randomId = new Random();
            return new UId(manufacturerId, productId, (uint)randomId.Next(1, 0x00FFFFFF));
        }

        /// <summary>
        /// 格式化 UID
        /// </summary>
        /// <param name="value">UID 16进制字符串</param>
        /// <returns>UID</returns>
        public static UId Parse(string value)
        {
            string[] parts = value.Split(':');
            return new UId((ushort) int.Parse(parts[0], System.Globalization.NumberStyles.HexNumber), (uint) int.Parse(parts[1], System.Globalization.NumberStyles.HexNumber));
        }

        /// <summary>
        /// 格式化 Url UID
        /// </summary>
        /// <param name="url">Url 格式 16进制字符串</param>
        /// <returns>UID</returns>
        public static UId ParseUrl(string url)
        {           
            string[] parts = url.Split('/');
            string idPart = parts[parts.Length - 1];
            
            // 格式化字符串
            idPart = idPart.Replace("0x",string.Empty).Replace(":",string.Empty);

            return new UId((ushort) int.Parse(idPart.Substring(0, 4), System.Globalization.NumberStyles.HexNumber), (uint) int.Parse(idPart.Substring(4, 8), System.Globalization.NumberStyles.HexNumber));
        }

        /// <summary>
        /// 获取 UID 哈希值
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return ManufacturerId.GetHashCode() + DeviceId.GetHashCode();
        }

        /// <summary>
        /// UID 比较
        /// </summary>
        /// <param name="obj">要比较的 UID</param>
        /// <returns>是否一致</returns>
        public override bool Equals(object obj)
        {
            UId id = obj as UId;
            if (!object.ReferenceEquals(id, null))
                return id.ManufacturerId.Equals(ManufacturerId) && id.DeviceId.Equals(DeviceId);

            return base.Equals(obj);
        }

        #region IComparable Members

        public int CompareTo(object obj)
        {
            UId id = obj as UId;

            if (id != null)
                return ManufacturerId.CompareTo(id.ManufacturerId) + DeviceId.CompareTo(id.DeviceId);

            return -1;
        }

        #endregion
    }
}
