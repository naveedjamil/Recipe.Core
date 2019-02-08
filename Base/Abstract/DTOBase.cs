using System;
using System.Collections.Generic;
using System.Text;

namespace Recipe.NetCore.Base.Abstract
{
    public class DTOBase
    {
        public bool HasErrors { get; set; }

        public Exception Error { get; set; }
    }
}
