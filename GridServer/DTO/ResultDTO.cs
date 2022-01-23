﻿using System;

namespace GridServer.DTO
{
    internal class ResultDto
    {
        public JsonType.RequestType DataType = JsonType.RequestType.Result;
        public Guid Id;
        public int Symbols;
        public int Spaces;
    }
}
