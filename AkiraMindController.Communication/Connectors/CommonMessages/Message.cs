﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AkiraMindController.Communication.Connectors.InternalMessages
{
    [Serializable]
    public class Message
    {
        public Message() { }

        public Message(string v)
        {
            content = v;
        }

        public string content;
    }
}
