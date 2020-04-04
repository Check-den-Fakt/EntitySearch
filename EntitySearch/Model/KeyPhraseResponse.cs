using System;
using System.Collections.Generic;
using System.Text;

namespace Entity.Model
{
    public class KeyPhraseResponse
    {
        public string Text { get; set; }
        public string Language { get; set; }

        public KeyPhraseResponse(string language)
        {
            Language = language;
        }
    }

    
}
