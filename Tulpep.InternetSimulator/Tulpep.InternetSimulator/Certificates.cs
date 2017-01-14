using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management.Automation;
using System.Linq;

namespace Tulpep.InternetSimulator
{
    class Certificates
    {

        private const string SSL_FRIENDLY_NAME = "Internet Simulator";
        public string CertHash { get; set; }

        public Certificates(IEnumerable<string> domains)
        {
            if(domains.Count() > 0)
                CertHash = CreateAndInstallCertificate(domains);
        }

        public bool RemoveCertificates()
        {
            var powerShell = PowerShell.Create();

            string removeCertScript = string.Format(@"
                    Get-ChildItem Cert:\LocalMachine\My | Where {{ $_.FriendlyName -match ""Internet Simulator""}} | Remove-Item
                    Get-ChildItem Cert:\LocalMachine\Root | Where {{ $_.FriendlyName -match ""Internet Simulator""}} | Remove-Item
                    ", SSL_FRIENDLY_NAME);


            powerShell.AddScript(removeCertScript).Invoke();
            if (powerShell.HadErrors)
            {
                Logging.WriteVerbose("Cannot remove SSL generated certificates from the system");
                return false;
            }
            else
            {
                Logging.WriteVerbose("SLL generated certificates has been remove from the System");
                return true;
            }
        }

        public bool AddSSLBinding()
        {
            RemoveSSLBinding();

            Process process = new Process();
            process.StartInfo.FileName = "netsh";
            process.StartInfo.Arguments = string.Format("http add sslcert ipport=0.0.0.0:443 certhash={0} appid={{{1}}}", CertHash, Guid.NewGuid());
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();
            process.WaitForExit();
            if (process.ExitCode == 0)
            {
                Logging.WriteVerbose("Added SSL Bindings with generated certificates to 443 Port");
                return true;
            }
            return false;
        }

        public bool RemoveSSLBinding()
        {
            Process process = new Process();
            process.StartInfo.FileName = "netsh";
            process.StartInfo.Arguments = "http delete sslcert ipport=0.0.0.0:443";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();
            process.WaitForExit();
            if (process.ExitCode == 0)
            {
                Logging.WriteVerbose("SSL binding of port 443 has been removed");
                return true;
            }
            return false;
        }


        private string CreateAndInstallCertificate(IEnumerable<string> domains)
        {
            //Create certificate in Computer Personal store
            string createCertificateScript = string.Format(@"New-SelfSignedCertificate -DnsName {0} -CertStoreLocation {1} -FriendlyName ""{2}""",
                                        string.Join(",", domains),
                                        @"Cert:\LocalMachine\My",
                                        SSL_FRIENDLY_NAME);

            var createCertificatesPs = PowerShell.Create();
            var createCertifiacateResult = createCertificatesPs.AddScript(createCertificateScript).Invoke();
            if (createCertificatesPs.HadErrors) return String.Empty;
            string certHash = createCertifiacateResult[0].Properties["Thumbprint"].Value.ToString();

            Logging.WriteVerbose("SLL Certificate saved in your Computer Personal Store. Domains: {0}. Thumbprint: {1}.",
                           string.Join(", ", domains),
                           certHash);

            string copyCertScript = string.Format(@"
                    $srcStore = New-Object System.Security.Cryptography.X509Certificates.X509Store ""My"", ""LocalMachine""
                    $srcStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadOnly)
                    $cert =  $srcStore.certificates -match ""{0}""

                    $dstStore = New-Object System.Security.Cryptography.X509Certificates.X509Store ""Root"", ""LocalMachine""
                    $dstStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
                    $dstStore.Add($cert[0])

                    $srcStore.Close
                    $dstStore.Close

                    ", certHash);

            var AddCertificateToRootPs = PowerShell.Create();
            AddCertificateToRootPs.AddScript(copyCertScript).Invoke();
            if (AddCertificateToRootPs.HadErrors) return String.Empty;

            Logging.WriteVerbose("SLL Certificate added to Trusted Root Certification Authorities");
            return certHash;
        }


    }
}
