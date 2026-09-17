const replicaSetName = process.env.MONGO_REPLICA_SET || 'rs0';
const memberHost = process.env.MONGO_MEMBER_HOST || 'mongo:27017';
const appDatabase = process.env.MONGO_APP_DATABASE;
const appUser = process.env.MONGO_APP_USERNAME;
const appPassword = process.env.MONGO_APP_PASSWORD;

let initiated = false;

try {
  rs.status();
  initiated = true;
} catch (error) {
  if (error.codeName !== 'NotYetInitialized') {
    throw error;
  }
}

if (!initiated) {
  rs.initiate({ _id: replicaSetName, members: [{ _id: 0, host: memberHost }] });
  print('replica set initiated ' + replicaSetName);
}

let attempts = 0;

while (!db.hello().isWritablePrimary) {
  attempts += 1;

  if (attempts > 60) {
    throw new Error('replica set did not elect primary');
  }

  sleep(1000);
}

if (appDatabase && appUser && appPassword) {
  const target = db.getSiblingDB(appDatabase);

  if (target.getUser(appUser) === null) {
    target.createUser({ user: appUser, pwd: appPassword, roles: [{ role: 'readWrite', db: appDatabase }] });
    print('app user created ' + appUser + ' on ' + appDatabase);
  }
}

print('mongo init done');
