﻿//
// Copyright (c) 2018 The TurtleCoin Developers
// 
// Please see the included LICENSE file for more information.

using System;
using System.IO;
using System.Linq;

using Canti.Data;
using Canti.Errors;
using Canti.Utilities;
using Canti.Blockchain;
using Canti.Blockchain.Crypto;
using Canti.Blockchain.Crypto.Mnemonics;
using Canti.Blockchain.WalletBackend;

namespace CLIWallet
{
    public static class LaunchWallet 
    {
        /* Handle the inputted action, and either return a wallet backend, or
           an error */
        public static IEither<Error, WalletBackend>
                      HandleAction(LaunchAction action)
        {
            switch(action)
            {
                case LaunchAction.Open:
                {
                    return OpenWallet(); 
                }
                case LaunchAction.Create:
                {
                    return CreateWallet();
                }
                case LaunchAction.SeedRestore:
                {
                    /* We can create a wallet directly from a seed - but
                       we'd rather alert the user whenever a single part of
                       the input is incorrect, rather than getting them to
                       enter the walletname, walletpass, and seed, and then
                       alerting one bit is wrong */
                    PrivateKeys keys = GetPrivateKeysFromSeed();
                    return CreateWallet(keys);
                }
                case LaunchAction.KeyRestore:
                {
                    PrivateKeys keys = GetPrivateKeys();
                    return CreateWallet(keys);
                }
                case LaunchAction.ViewWallet:
                {
                    PrivateKey privateViewKey
                        = GetPrivateKey("Private view key: ");

                    string address = GetAddress();
                    return CreateWallet(privateViewKey, address);
                }
                default:
                {
                    throw new ArgumentException(
                        "Programmer error: Unhandled action!"
                    );
                }
            }
        }

        private static string GetAddress()
        {
            while (true)
            {
                ConsoleMessage.Write(ConsoleColor.Yellow, $"Public {Globals.ticker} address: ");

                string input = Console.ReadLine();

                /* Try and parse into public keys to see if it's valid, don't
                   need the keys so ignore the result. */
                switch(Addresses.KeysFromAddress(input))
                {
                    case ILeft<Error> error:
                    {
                        ConsoleMessage.WriteLine(ConsoleColor.Red, error.Value.errorMessage + "\n");
                        continue;
                    }
                    default:
                    {
                        return input;
                    }
                }
            }
        }

        private static PrivateKey GetPrivateKey(string msg)
        {
            while (true)
            {
                ConsoleMessage.Write(ConsoleColor.Yellow, msg);
                string input = Console.ReadLine();

                if (input.Length != 64)
                {
                    ConsoleMessage.WriteLine(ConsoleColor.Red, "Invalid private key, should be 64 characters long! Try again.\n");

                    continue;
                }

                if (!input.IsHex())
                {
                    ConsoleMessage.WriteLine(ConsoleColor.Red, "Invalid private key, is not a valid hexadecimal string!");

                    continue;
                }

                var p = new PrivateKey(Encoding.HexStringToByteArray(input));

                if (!KeyOps.IsValidKey(p))
                {
                    ConsoleMessage.WriteLine(ConsoleColor.Red, "Invalid private key, is not a valid ED25519 key!");
                }

                return p;
            }
        }

        private static PrivateKeys GetPrivateKeys()
        {
            return new PrivateKeys(GetPrivateKey("Private spend key: "),
                                   GetPrivateKey("Private view key: "));
        }

        private static PrivateKeys GetPrivateKeysFromSeed()
        {
            while (true)
            {
                ConsoleMessage.Write(ConsoleColor.Yellow, "Mnemonic seed (25 words): ");
                string input = Console.ReadLine();

                switch (Mnemonics.MnemonicToPrivateKey(input))
                {
                    case ILeft<Error> error:
                    {
                        ConsoleMessage.WriteLine(ConsoleColor.Red, error.Value.errorMessage);
                        ConsoleMessage.WriteLine("Try again.\n");
                        continue;
                    }
                    case IRight<PrivateKey> key:
                    {
                        var privateSpendKey = key.Value;

                        var privateViewKey
                            = KeyOps.GenerateDeterministicKeys(privateSpendKey)
                                    .privateKey;

                        return new PrivateKeys(privateSpendKey, privateViewKey);
                    }
                }
            }
        }

        /* Get the filename to use for a new wallet */
        private static string GetNewWalletName()
        {
            while (true)
            {
                ConsoleMessage.Write(ConsoleColor.Yellow, "What filename would you like to give your new wallet?: ");

                string filename = Console.ReadLine();

                string appended = filename + ".wallet";

                if (string.IsNullOrWhiteSpace(filename))
                {
                    ConsoleMessage.WriteLine(ConsoleColor.Red, "Wallet name cannot be empty! Try again.\n");

                    continue;
                }

                if (GeneralUtilities.FilenameInUse(filename) ||
                    GeneralUtilities.FilenameInUse(appended))
                {
                    ConsoleMessage.Write(ConsoleColor.Red, "A file with the name ");
                    ConsoleMessage.Write(ConsoleColor.Yellow, filename);
                    ConsoleMessage.Write(ConsoleColor.Red, " or ");
                    ConsoleMessage.Write(ConsoleColor.Yellow, appended);
                    ConsoleMessage.WriteLine(ConsoleColor.Red, " already exists! Try again.\n");
                }
                /* If the file already ends with .wallet, return it. */
                else if (filename.EndsWith(".wallet"))
                {
                    return filename;
                }
                /* Else, append .wallet to the filename */
                else
                {
                    return appended;
                }
            }
        }

        /* Get the password for an already existing wallet (no confirmation) */
        private static string GetWalletPassword()
        {
            ConsoleMessage.Write(ConsoleColor.Yellow, "Enter your wallet password: ");
            return Console.ReadLine();
        }

        /* Get the password for a new wallet file (Confirm the password) */
        private static string GetNewWalletPassword()
        {
            while (true)
            {
                ConsoleMessage.Write(ConsoleColor.Yellow, "Give your new wallet a password: ");

                string firstPassword = Console.ReadLine();

                ConsoleMessage.Write(ConsoleColor.Yellow, "Confirm your new password: ");

                string secondPassword = Console.ReadLine();

                if (firstPassword != secondPassword)
                {
                    ConsoleMessage.WriteLine(ConsoleColor.Red, "Passwords do not match! Try again.\n");
                }
                else
                {
                    return firstPassword;
                }
            }
        }

        /* Get the filename of an already existing wallet */
        private static string GetExistingWalletName()
        {
            while (true)
            {
                ConsoleMessage.Write(ConsoleColor.Yellow, "What wallet filename would you like to open?: ");

                string filename = Console.ReadLine();

                string appended = filename + ".wallet";

                if (string.IsNullOrWhiteSpace(filename))
                {
                    ConsoleMessage.WriteLine(ConsoleColor.Red, "Wallet name cannot be empty! Try again.");
                    continue;
                }

                if (File.Exists(filename))
                {
                    return filename;
                }
                /* Automatically add the .wallet extension for users */
                else if (File.Exists(appended))
                {
                    return appended;
                }
                else
                {
                    ConsoleMessage.Write(ConsoleColor.Red, "A file with the name ");
                    ConsoleMessage.Write(ConsoleColor.Yellow, filename);
                    ConsoleMessage.Write(ConsoleColor.Red, " or ");
                    ConsoleMessage.Write(ConsoleColor.Yellow, appended);
                    ConsoleMessage.WriteLine(ConsoleColor.Red, " doesn't exist!");
                    ConsoleMessage.WriteLine("Ensure you entered your wallet name correctly.\n");
                }
            }
        }

        /* Attempts to open a wallet, and either returns the wallet, or
           an Error */
        private static IEither<Error, WalletBackend> OpenWallet()
        {
            string walletFilename = GetExistingWalletName();

            while (true)
            {
                string walletPassword = GetWalletPassword();

                var maybeWalletBackend = WalletBackend.Load(
                    walletFilename, walletPassword
                );

                if (maybeWalletBackend.IsLeft())
                {
                    Error err = maybeWalletBackend.Left();

                    /* If the password is incorrect, try again. Else, return
                       the error. */
                    if (err.errorName == "INCORRECT_PASSWORD")
                    {
                        ConsoleMessage.WriteLine(ConsoleColor.Red, err.errorMessage + "\n");
                        continue;
                    }
                }
                
                return maybeWalletBackend;
            }
        }

        /* Creates a wallet from the given private keys, and either returns
           a walletbackend, or an error */
        private static IEither<Error, WalletBackend>
                       CreateWallet(PrivateKeys privateKeys)
        {
            string walletFilename = GetNewWalletName();
            string walletPassword = GetNewWalletPassword();

            return WalletBackend.NewWallet(walletFilename, walletPassword);
        }
        
        /* Creates a view wallet from the given private view key and public
           keys, or returns an error */
        private static IEither<Error, WalletBackend>
                       CreateWallet(PrivateKey privateViewKey,
                                    string address)
        {
            string walletFilename = GetNewWalletName();
            string walletPassword = GetNewWalletPassword();

            return WalletBackend.NewWallet(
                walletFilename, walletPassword, privateViewKey, address
            );
        }

        /* Creates a new wallet, and returns either a wallet backend, or
           an error */
        private static IEither<Error, WalletBackend> CreateWallet()
        {
            return CreateWallet(KeyOps.GenerateWalletKeys().GetPrivateKeys());
        }
    }
}
