using System;
using System.IO;
using UnityEngine;
using System.Xml;

namespace YsoCorp {
    namespace GameUtils {
        public static class YCXmlHandler {

            public static void UpdateDocument(string filePath, Action<XmlDocument, XmlElement> updates) {
                string path = Path.Combine(Application.dataPath, filePath);
                XmlDocument xmlDocument = new XmlDocument();
                xmlDocument.Load(path);
                XmlElement documentRoot = xmlDocument.DocumentElement;

                updates?.Invoke(xmlDocument, documentRoot);
                xmlDocument.Save(path);
            }

            public static void CreateElement(XmlDocument xmlDocument, XmlElement documentRoot, YCXmlElement ycXmlElement, bool insertBefore = false) {
                if (GetExistingNode(documentRoot, ycXmlElement) == null) {
                    XmlNode elementRoot = GetElementRoot(documentRoot, ycXmlElement);
                    XmlNodeList refElements = elementRoot.SelectNodes("descendant::" + ycXmlElement.elementName);

                    XmlElement element = xmlDocument.CreateElement(ycXmlElement.elementName);
                    SetAttributes(element, ycXmlElement.attributes);

                    if (refElements.Count > 0) {
                        if (insertBefore) {
                            elementRoot.InsertBefore(element, refElements[0]);
                        } else {
                            elementRoot.InsertAfter(element, refElements[refElements.Count - 1]);
                        }
                    } else {
                        elementRoot.AppendChild(element);
                    }
                }
            }

            public static void UpdateElement(XmlDocument xmlDocument, XmlElement documentRoot, YCXmlElement ycXmlElement, YCXmlAttribute[] newAttributes, bool createIfNotFound = false, bool insertBefore = false) {
                XmlElement element = (XmlElement)GetExistingNode(documentRoot, ycXmlElement);

                if (element != null) {
                    SetAttributes(element, newAttributes);
                } else if (createIfNotFound) {
                    ycXmlElement.attributes = newAttributes;
                    CreateElement(xmlDocument, documentRoot, ycXmlElement, insertBefore);

                }
            }

            public static void DeleteNode(XmlElement documentRoot, YCXmlElement ycXmlElement) {
                XmlNode node = GetExistingNode(documentRoot, ycXmlElement);

                if (node != null) {
                    node.ParentNode.RemoveChild(node);
                }
            }

            public static void SetAttributes(XmlElement element, YCXmlAttribute[] attributes) {
                foreach (YCXmlAttribute attribute in attributes) {
                    SetAttribute(element, attribute);
                }
            }

            public static void SetAttribute(XmlElement element, YCXmlAttribute attribute) {
                switch (attribute.ycAttributeType) {
                    case YCAttributeType.Android:
                        element.SetAttribute(attribute.name, "http://schemas.android.com/apk/res/android", attribute.value);
                        break;
                    case YCAttributeType.Xmlns:
                        element.SetAttribute("xmlns:" + attribute.name, attribute.value);
                        break;
                    case YCAttributeType.Tools:
                        element.SetAttribute(attribute.name, "http://schemas.android.com/tools", attribute.value);
                        break;
                    default:
                        element.SetAttribute(attribute.name, attribute.value);
                        break;
                }
            }

            public static XmlNode GetElementRoot(XmlElement documentRoot, YCXmlElement ycXmlElement) {
                if (ycXmlElement.rootPath == "") {
                    return documentRoot;
                } else {
                    if (ycXmlElement.rootPath[0] != '/') {
                        Debug.LogError("Root Path must start with a\'/\'");
                        return null;
                    }
                    return documentRoot.SelectSingleNode(ycXmlElement.rootPath);
                }
            }

            public static XmlNode GetExistingNode(XmlElement documentRoot, YCXmlElement ycXmlElement) {
                XmlNode elementRoot = GetElementRoot(documentRoot, ycXmlElement);

                string xPath = ycXmlElement.elementName + "[";
                for (int i = 0; i < ycXmlElement.attributes.Length; i++) {
                    if (i > 0) {
                        xPath += " and ";
                    }
                    xPath += "@*[local-name()=\'" + ycXmlElement.attributes[i].name + "\']=\'" + ycXmlElement.attributes[i].value + "\'";
                }
                xPath += "]";

                return elementRoot.SelectSingleNode(xPath);
            }

            public struct YCXmlElement {
                public string rootPath;
                public string elementName;
                public YCXmlAttribute[] attributes;

                public YCXmlElement(string _rootPath, string _elementName, params YCXmlAttribute[] _attributes) {
                    this.rootPath = _rootPath;
                    this.elementName = _elementName;
                    this.attributes = _attributes;
                }
            }

            public struct YCXmlAttribute {
                public string name;
                public YCAttributeType ycAttributeType;
                public string ycAttributeTypeName;
                public string value;

                public YCXmlAttribute(string _name, YCAttributeType _ycAttributeType, string _value) {
                    this.name = _name;
                    this.ycAttributeType = _ycAttributeType;
                    this.ycAttributeTypeName = "";
                    this.value = _value;
                }

                public YCXmlAttribute(string _name, string _ycAttributeTypeName, string _value) {
                    this.name = _name;
                    this.ycAttributeType = YCAttributeType.None;
                    this.ycAttributeTypeName = _ycAttributeTypeName;
                    this.value = _value;
                }
            }

            public enum YCAttributeType {
                None,
                Android,
                Xmlns,
                Tools,
            }
        }
    }
}