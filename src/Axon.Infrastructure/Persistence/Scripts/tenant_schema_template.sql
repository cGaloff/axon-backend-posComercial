CREATE TABLE {SCHEMA_NAME}.roles (
    id UUID PRIMARY KEY,
    name VARCHAR(100) NOT NULL UNIQUE,
    is_system BOOLEAN NOT NULL DEFAULT false,
    description TEXT
);

CREATE TABLE {SCHEMA_NAME}.permissions (
    id UUID PRIMARY KEY,
    module VARCHAR(100) NOT NULL,
    action VARCHAR(100) NOT NULL,
    UNIQUE (module, action)
);

CREATE TABLE {SCHEMA_NAME}.role_permissions (
    role_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.roles(id),
    permission_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.permissions(id),
    PRIMARY KEY (role_id, permission_id)
);

CREATE TABLE {SCHEMA_NAME}.users (
    id UUID PRIMARY KEY,
    full_name VARCHAR(200) NOT NULL,
    email VARCHAR(200) NOT NULL UNIQUE,
    password_hash TEXT NOT NULL,
    role_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.roles(id),
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
