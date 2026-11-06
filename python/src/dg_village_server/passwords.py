"""Password hashing with scrypt from the standard library (no extra dependency).

Stored format: "scrypt$<n>$<r>$<p>$<salt hex>$<hash hex>", so the cost can be raised later
without breaking existing hashes.
"""

import hashlib
import hmac
import secrets

_N, _R, _P = 2**14, 8, 1  # ~16 MB and a few tens of ms per hash
_SALT_BYTES = 16
_HASH_BYTES = 32


def _scrypt(password: str, salt: bytes, n: int, r: int, p: int) -> bytes:
    return hashlib.scrypt(
        password.encode(), salt=salt, n=n, r=r, p=p, maxmem=64 * 1024 * 1024, dklen=_HASH_BYTES
    )


def hash_password(password: str) -> str:
    salt = secrets.token_bytes(_SALT_BYTES)
    digest = _scrypt(password, salt, _N, _R, _P)
    return f"scrypt${_N}${_R}${_P}${salt.hex()}${digest.hex()}"


def verify_password(password: str, stored: str) -> bool:
    try:
        scheme, n, r, p, salt_hex, digest_hex = stored.split("$")
        if scheme != "scrypt":
            return False
        digest = _scrypt(password, bytes.fromhex(salt_hex), int(n), int(r), int(p))
    except ValueError:
        return False
    return hmac.compare_digest(digest, bytes.fromhex(digest_hex))
