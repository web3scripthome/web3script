using System;
using System.Collections.Generic;

namespace web3script.Models
{
    public class Wallet
    {
        public string Id { get; set; }
        public string Address { get; set; }
        public string Mnemonic { get; set; }
        public string PrivateKey { get; set; }
        public string Remark { get; set; }
        public bool IsSelected { get; set; }

        public Wallet()
        {
            Id = Guid.NewGuid().ToString();
            IsSelected = false;
        }
    }
    public class suiWallet
    {
        public string Id { get; set; }
        public string Mnemonic { get; set; }
        public string Address { get; set; } // 0x...
        public string SuiPrivateKey { get; set; } // suiprivkey1...
        public string PrivateKeyHex { get; set; } // 64λ Hex
        public string PublicKeyHex { get; set; }  // 64λ Hex
        public string Remark { get; set; }
        public bool IsSelected { get; set; }
        public suiWallet()
        {
            Id = Guid.NewGuid().ToString();
            IsSelected = false;
        }
    }
    public class WalletGroup
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<string> WalletIds { get; set; }

        public WalletGroup()
        {
            Id = Guid.NewGuid().ToString();
            WalletIds = new List<string>();
        }
    }
} 