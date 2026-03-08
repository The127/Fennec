# Authentication

This document explains the authentication processes and ideas in Fennec.

## Registration

A user can register with an instance of fennec as long as the instance is configured to allow registration.

## Login

A user can only log in with the instance that they registered with (home instance).
Login is done by sending a POST request containing the username and password.
As a result, a new session is created in the database and the session token is returned in the response body.
The session token is long-lived and only used to request a public token from the home instance.

## Acquiring a public token

A public token is a short-lived token that can be used to authenticate with the user with any instance.
To acquire a public token, the user sends a POST request to the home instance with the session token and the target instance url.
This will return a JWT signed by the home instance, with the home instance as the issuer and has the target instance url as the audience.
This way the public token can only be used with the target instance.
The public token is used to authenticate with the target instance when making requests.
The same mechanism is used to authenticate with the home instance when making requests.

## Verifying a public token

An instance verifies a public token by checking if the token is valid and if the audience matches the instance url.
The instance then acquires the public key from the issuer of the JWT and uses it to verify the token.
This way authentication is secure, fast and scalable in a distributed environment.
