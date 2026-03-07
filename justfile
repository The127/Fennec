# generate a local keypair for development purposes
generate-keypair:
    # generate private key
    openssl genrsa -out private.pem 2048
    # generate public key
    openssl rsa -in private.pem -pubout -out public.pem