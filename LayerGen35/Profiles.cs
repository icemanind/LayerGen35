using System.Collections.Generic;
using System.IO;

namespace LayerGen35
{
    public class Profiles : List<Profile>
    {
        public static string ToXml(Profiles profiles)
        {
            var x = new System.Xml.Serialization.XmlSerializer(profiles.GetType());
            using (var sw = new StringWriter())
            {
                x.Serialize(sw, profiles);
                return sw.ToString();
            }
        }

        public static Profiles FromXml(string xml)
        {
            var x = new System.Xml.Serialization.XmlSerializer(typeof(Profiles));

            using (var sr = new StringReader(xml))
            {
                return (Profiles)x.Deserialize(sr);
            }
        }

        public string ToXml()
        {
            return ToXml(this);
        }
    }
}
