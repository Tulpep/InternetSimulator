Internet Simulator [![Build Status](https://ci.appveyor.com/api/projects/status/github/Tulpep/InternetSimulator)](https://ci.appveyor.com/project/tulpep/InternetSimulator)
===========

###A very simple utily to fully simulate an Internet Connection and make Windows think it is connected
![NCSI](Screenshots/ncsi.png)

##[Download now](https://github.com/Tulpep/InternetSimulator/releases/latest)

Using this very simple Windows command line utility you to simulate and Internet connection. You chose what domains will be simulated and the rest will go to the *real internet*. You can use it for:

1. Phishing and DNS pharming demos
2. Allow the use of software that require and active Internet connection
3. Tricking Windows icons to show you are connected even when you are fully offline
4. Installing software that use an online installer



When running Internet Simulator, it will:

1. Create SSL certificates for the domains you are trying to simulate and saving it in your Trusted Root Certficate Authorities.
2. Start a self hosted DNS server for simulation purposes. It does not rely on 'hosts' file so you can simulate every domain including Microsoft official domains
3. Start a self hosted HTTP and HTTPS servers. HTTPS server will use the generated certificates.
4. Simulate Microsoft Network Connectivity Status Indicator (NCSI) domains so Windows will show its status as Connected to the Internet
5. Backup your Network Cards DNS configuration and change ther DNS to `127.0.0.1`. This will start filtering the traffic throught the simulator

When you stop it, it will:

1. Stop DNS, HTTP and HTTPS self hosted services
2. Delete the temporary generated certificates from your Personal Store and your Trusted Root CA. So no traces in your System.
3. Restore the Network Interfaces configuration to their original settings


**Thanks for tying it. We are active supporting this tool and are open to your PRs :smile:**

