# IMAP HEALTH CHECK

![](https://img.shields.io/github/downloads/oliverabraham/EmailHealthCheck/total) ![](https://img.shields.io/github/license/oliverabraham/EmailHealthCheck) ![](https://img.shields.io/github/languages/count/oliverabraham/EmailHealthCheck) ![GitHub Repo stars](https://img.shields.io/github/stars/oliverabraham/EmailHealthCheck?label=repo%20stars) ![GitHub Repo stars](https://img.shields.io/github/stars/oliverabraham?label=user%20stars)


## OVERVIEW

This is a monitor that searches for the newest email from a person.
It monitors the age of that email, verifying we get a health signal from that person.


## FUNCTION

It will connect periodcally to your imap mail server and check every new(unread) email.
If will select emails from a certain person, optionally having some whitelisted word in subject.
Out of these emails, it will pick the newest one and calculate the age in days.

The configuration must be made in the appsettings.hjson file.

## AUTHOR
Written by Oliver Abraham, mail@oliver-abraham.de


## INSTALLATION
An installer is not provided. Build the application or download the latest release


## CONFIGURATION

Just edit the file appsettings.hjson. 



## LICENSE
This project is licensed under Apache license.


## SOURCE CODE
https://www.github.com/OliverAbraham/EmailHealthCheck


## AUTHOR
Oliver Abraham, mail@oliver-abraham.de


# MAKE A DONATION !

If you find this application useful, buy me a coffee!
I would appreciate a small donation on https://www.buymeacoffee.com/oliverabraham

<a href="https://www.buymeacoffee.com/app/oliverabraham" target="_blank"><img src="https://cdn.buymeacoffee.com/buttons/v2/default-yellow.png" alt="Buy Me A Coffee" style="height: 60px !important;width: 217px !important;" ></a>
